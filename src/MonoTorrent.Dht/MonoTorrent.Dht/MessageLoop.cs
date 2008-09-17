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
using MonoTorrent.Common;

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

        internal event EventHandler<SendQueryEventArgs> QuerySent;

        List<IAsyncResult> activeSends = new List<IAsyncResult>();
        DhtEngine engine;
        int lastSent;
        IListener listener;
        private object locker = new object();
        Queue<SendDetails> sendQueue = new Queue<SendDetails>();
        Queue<KeyValuePair<IPEndPoint, Message>> receiveQueue = new Queue<KeyValuePair<IPEndPoint, Message>>();
        MonoTorrentCollection<SendDetails> waitingResponse = new MonoTorrentCollection<SendDetails>();
        
        private bool CanSend
        {
            get { return activeSends.Count < 5 && sendQueue.Count > 0 && (Environment.TickCount - lastSent) > 5; }
        }

        public MessageLoop(DhtEngine engine, IListener listener)
        {
            this.engine = engine;
            this.listener = listener;
            listener.MessageReceived += new MessageReceived(MessageReceived);
            DhtEngine.MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(5), delegate {
                SendMessage();
                ReceiveMessage();
                TimeoutMessage();
                return true;
            });
        }

        void MessageReceived(byte[] buffer, IPEndPoint endpoint)
        {
            lock (locker)
            {
                // I should check the IP address matches as well as the transaction id
                // FIXME: This should throw an exception if the message doesn't exist, we need to handle this
                // and return an error message (if that's what the spec allows)
                try
                {
                    Message m = MessageFactory.DecodeMessage((BEncodedDictionary)BEncodedValue.Decode(buffer));
                    receiveQueue.Enqueue(new KeyValuePair<IPEndPoint, Message>(endpoint, m));
                }
                catch (MessageException)
                {
                    // Caused by bad transaction id usually - ignore
                }
                catch (Exception)
                {
                    //throw new Exception("IP:" + endpoint.Address.ToString() + "bad transaction:" + e.Message);
                }
            }
        }

        private void RaiseMessageSent(IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
        {
            EventHandler<SendQueryEventArgs> h = QuerySent;
            if (h != null)
                h(this, new SendQueryEventArgs(endpoint, query, response));
        }

        private void SendMessage()
        {
            SendDetails? send = null;
            if (CanSend)
                send = sendQueue.Dequeue();

            if (send != null)
            {
                SendMessage(send.Value.Message, send.Value.Destination);
                SendDetails details = send.Value;
                details.SentAt = DateTime.UtcNow;
                if (details.Message is QueryMessage)
                    waitingResponse.Add(details);
            }

        }

        private void TimeoutMessage()
        {
            if (waitingResponse.Count > 0)
            {
                if ((DateTime.UtcNow - waitingResponse[0].SentAt) > engine.TimeOut)
                {
                    SendDetails details = waitingResponse.Dequeue();
                    MessageFactory.UnregisterSend((QueryMessage)details.Message);
                    RaiseMessageSent(details.Destination, (QueryMessage)details.Message, null);
                }
            }
        }

        private void ReceiveMessage()
        {
            KeyValuePair<IPEndPoint, Message>? receive = null;

            if (receiveQueue.Count > 0)
                receive = receiveQueue.Dequeue();

            if (receive != null)
            {
                Message m = receive.Value.Value;
                IPEndPoint source = receive.Value.Key;
                waitingResponse.RemoveAll(delegate(SendDetails msg) {
                    return msg.Message.TransactionId.Equals(m.TransactionId);
                });

                Console.WriteLine("Received: {0} from {1}", m.GetType().Name, source);
                try
                {
                    Node node = engine.RoutingTable.FindNode(m.Id);

                    // What do i do with a null node?
                    if (node == null)
                    {
                        node = new Node(m.Id, source);
                        engine.RoutingTable.Add(node);
                    }
                    node.Seen();
                    Console.WriteLine("Seen {0}", node.Id.ToString());
                    m.Handle(engine, node);
                    ResponseMessage response = m as ResponseMessage;
                    if (response != null)
                    {
                        RaiseMessageSent(node.EndPoint, response.Query, response);
                    }
                }
                catch (MessageException)
                {
                    // Normal operation (FIXME: do i need to send a response error message?) 
                }
                catch
                {
                    Console.WriteLine("Handle Error for message: {0}", m);
                    this.EnqueueSend(new ErrorMessage(ErrorCode.GenericError, "Misshandle received message!"), source);
                }
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
            }
        }

        internal void EnqueueSend(Message message, Node node)
        {
            EnqueueSend(message, node.EndPoint);
        }
    }
}

