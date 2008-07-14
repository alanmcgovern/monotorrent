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
        int lastSent;
        IListener listener;
        private object locker = new object();
        Queue<SendDetails> sendQueue = new Queue<SendDetails>();
        Queue<KeyValuePair<IPEndPoint, Message>> receiveQueue = new Queue<KeyValuePair<IPEndPoint, Message>>();
        Thread thread;
        ManualResetEvent waitHandle = new ManualResetEvent(false);

        private bool CanSend
        {
            get { return activeSends.Count < 5 && sendQueue.Count > 0 && (Environment.TickCount - lastSent) > 5; }
        }

        public MessageLoop(DhtEngine engine, IListener listener)
        {
            this.engine = engine;
            this.listener = listener;
            listener.MessageReceived += new MessageReceived(MessageReceived);
            thread = new Thread(Loop);
            thread.IsBackground = true;

            thread.Start();
        }

        void MessageReceived(byte[] buffer, IPEndPoint endpoint)
        {
            lock (locker)
            {
                Message m = MessageFactory.DecodeMessage((BEncodedDictionary)BEncodedValue.Decode(buffer));
                // I should check the IP address matches as well as the transaction id
                if (m is ResponseMessage)
                {
                    // FIXME: Should an error message be sent back?
                    if (!MessageFactory.UnregisterSend(m))
                        return;
                }


                receiveQueue.Enqueue(new KeyValuePair<IPEndPoint, Message>(endpoint, m));
                waitHandle.Set();
            }
        }

        void Loop()
        {
            int lastTrigger = 0;
            Queue<KeyValuePair<DateTime, QueryMessage>> waitingResponse = new Queue<KeyValuePair<DateTime, QueryMessage>>();
            while (true)
            {
                KeyValuePair<IPEndPoint, Message>? receive = null;
                SendDetails? send = null;
                QueryMessage timedOut = null;

                if (engine.State != State.NotReady)
                {
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
                            if ((DateTime.Now - waitingResponse.Peek().Key).TotalMilliseconds > engine.TimeOut)
                            {
								timedOut = waitingResponse.Dequeue().Value;
							    if (MessageFactory.UnregisterSend(timedOut))
                                    timedOut = null;
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
                    {
                        Message m = receive.Value.Value;
                        IPEndPoint source = receive.Value.Key;
                        DhtEngine.MainLoop.Queue(delegate { 
                            Console.WriteLine("Received: {0} from {1}", m.GetType().Name, source);
                            try
                            {
                                m.Handle(engine, source);
                            }
                            catch (MessageException ex)
                            {
                                // Normal operation (FIXME: do i need to send a response error message?) 
                            }
                            catch
                            {
                                Console.WriteLine("Handle Error for message: {0}", m);
                                this.EnqueueSend(new ErrorMessage(eErrorCode.GenericError, "Misshandle received message!"), source);
                            }
                        });
                    }
                    if (timedOut != null)
                        timedOut.TimedOut(engine);

                    if ((Environment.TickCount - lastTrigger) > 1000)
                    {
                        lastTrigger = Environment.TickCount;
                        DhtEngine.MainLoop.QueueWait(delegate {
                            foreach (Bucket b in engine.RoutingTable.Buckets)
                            {
                                foreach (Node n in b.Nodes)
                                {
                                    if (!n.CurrentlyPinging && (n.State == NodeState.Unknown || n.State == NodeState.Questionable))
                                    {
                                        n.CurrentlyPinging = true;
                                        EnqueueSend(new Ping(n.Id), n.EndPoint);
                                    }
                                }
                            }
                        });
                    }
                }
                // Wait timeout milliseconds or 1000, whichever is lower
                waitHandle.WaitOne(5/*Math.Min(engine.TimeOut, 1000)*/, false);
            }
        }

        private void SendMessage(Message message, IPEndPoint endpoint)
        {
            Console.WriteLine("Sending: {0} to {1}", message.GetType().Name, endpoint);
            lastSent = Environment.TickCount;
            byte[] buffer = message.Encode();
            listener.Send(buffer, endpoint);
        }

        internal void EnqueueSend(Message message, IPEndPoint endpoint)
        {
            if (message.TransactionId == null)
            {
                if (message is QueryMessage)
                    message.TransactionId = TransactionId.NextId();
                else if (message is ResponseMessage)
                    throw new ArgumentException("Message must have a transaction id");
            }

            lock (locker)
            {
                // We need to be able to cancel a query message if we time out waiting for a response
                if (message is QueryMessage)
                    MessageFactory.RegisterSend((QueryMessage)message);

                sendQueue.Enqueue(new SendDetails(endpoint, message));
                waitHandle.Set();
            }
        }

        internal void EnqueueSend(Message message, Node node)
        {
            EnqueueSend(message, node.EndPoint);
        }
    }
}

