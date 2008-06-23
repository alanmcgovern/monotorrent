//
// MessageLoop.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Listeners;

namespace MonoTorrent.Dht
{
    public class MessageLoop
    {
        private struct SendDetails
        {
            public SendDetails(IPEndPoint destination, Message message)
            {
                Destination = destination;
                Message = message;
                SentAt = DateTime.MinValue;
            }
            public IPEndPoint Destination;
            public Message Message;
            public DateTime SentAt;
        }

        List<IAsyncResult> activeSends = new List<IAsyncResult>();
        DhtEngine engine;
        IListener listener;
        private object locker = new object();
        Queue<SendDetails> sendQueue = new Queue<SendDetails>();
        Queue<KeyValuePair<IPEndPoint, Message>> receiveQueue = new Queue<KeyValuePair<IPEndPoint, Message>>();
        Thread thread;
        ManualResetEvent waitHandle = new ManualResetEvent(false);

        Dictionary<BEncodedString, QueryMessage> messages = new Dictionary<BEncodedString, QueryMessage>();

        private bool CanSend
        {
            get { return activeSends.Count < 5 && sendQueue.Count > 0; }
        }

        public MessageLoop(DhtEngine engine, IListener listener)
        {
            this.engine = engine;
            this.listener = listener;
            listener.MessageReceived += new MessageReceived(MessageReceived);
            thread = new Thread(Loop);
            thread.IsBackground = true;

            thread.Start();
            if (!listener.Started)
                listener.Start();
        }

        void MessageReceived(Message m, IPEndPoint endpoint)
        {
            lock (locker)
            {
                // I should check the IP address matches as well as the transaction id
                if (!messages.ContainsKey(m.TransactionId))
                    return;

                messages.Remove(m.TransactionId);
                receiveQueue.Enqueue(new KeyValuePair<IPEndPoint, Message>(endpoint, m));
                waitHandle.Set();
            }
        }

        void Loop()
        {
            Queue<KeyValuePair<DateTime, QueryMessage>> waitingResponse = new Queue<KeyValuePair<DateTime, QueryMessage>>();
            while (true)
            {
                KeyValuePair<IPEndPoint, Message>? receive = null;
                SendDetails? send = null;
                QueryMessage timedOut = null;

                lock (locker)
                {
                    if (CanSend)
                        send = sendQueue.Dequeue();

                    if (receiveQueue.Count > 0)
                        receive = receiveQueue.Dequeue();

                    if (receiveQueue.Count == 0 && !CanSend)
                        waitHandle.Reset();

                    if (waitingResponse.Count > 0)
                    {
                        if (!messages.ContainsKey(waitingResponse.Peek().Value.TransactionId))
                        {
                            waitingResponse.Dequeue();
                        }
                        else if ((DateTime.Now - waitingResponse.Peek().Key).TotalMilliseconds > engine.TimeOut)
                        {
                            timedOut = waitingResponse.Dequeue().Value;
                            messages.Remove(timedOut.TransactionId);
                        }
                    }
                }

                if (send != null)
                {
                    SendMessage(send.Value.Message, send.Value.Destination);
                    if (send.Value.Message is QueryMessage)
                        waitingResponse.Enqueue(new KeyValuePair<DateTime, QueryMessage>(DateTime.Now, (QueryMessage)send.Value.Message));
                }

                if (receive != null)
                    receive.Value.Value.Handle(engine, receive.Value.Key);

                if (timedOut != null)
                    timedOut.TimedOut(engine);

                // Wait timeout milliseconds or 1000, whichever is lower
                waitHandle.WaitOne(Math.Min(engine.TimeOut, 1000), false);
            }
        }

        private void SendMessage(Message message, IPEndPoint endpoint)
        {
            byte[] buffer = message.Encode();
            listener.Send(buffer, endpoint);
        }

        internal void EnqueueSend(Message message, IPEndPoint endpoint)
        {
            if (message.TransactionId == null)
                throw new ArgumentException("Message must have a transaction id");
            
            lock (locker)
            {
                // We need to be able to cancel a query message if we time out waiting for a response
                if(message is QueryMessage)
                    messages.Add(message.TransactionId, (QueryMessage) message);

                sendQueue.Enqueue(new SendDetails(endpoint, message));
                waitHandle.Set();
            }
        }

        internal void EnqueueSend(Message message, Node node)
        {
            EnqueueSend(message, node.ContactInfo.EndPoint);
        }
    }
}

