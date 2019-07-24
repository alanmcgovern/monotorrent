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
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Common;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    internal class MessageLoop
    {
        private struct SendDetails
        {
            public SendDetails(IPEndPoint destination, Message message, System.Threading.Tasks.TaskCompletionSource<SendQueryEventArgs> tcs)
            {
                CompletionSource = tcs;
                Destination = destination;
                Message = message;
                SentAt = DateTime.MinValue;
            }
            public System.Threading.Tasks.TaskCompletionSource<SendQueryEventArgs> CompletionSource;
            public IPEndPoint Destination;
            public Message Message;
            public DateTime SentAt;
        }

        internal event Action<object, SendQueryEventArgs> QuerySent;

        DhtEngine engine;
        DateTime lastSent;
        DhtListener listener;
        private object locker = new object();
        Queue<SendDetails> sendQueue = new Queue<SendDetails>();
        Queue<KeyValuePair<IPEndPoint, Message>> receiveQueue = new Queue<KeyValuePair<IPEndPoint, Message>>();
        Dictionary<BEncodedValue, SendDetails> waitingResponse = new Dictionary<BEncodedValue, SendDetails>();
        List<SendDetails> waitingResponseTimedOut = new List<SendDetails> ();
        
        private bool CanSend
        {
            get { return sendQueue.Count > 0 && (DateTime.UtcNow - lastSent) > TimeSpan.FromMilliseconds(5); }
        }

        public MessageLoop(DhtEngine engine, DhtListener listener)
        {
            this.engine = engine;
            this.listener = listener;
            listener.MessageReceived += MessageReceived;
            DhtEngine.MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(5), delegate {
                if (engine.Disposed)
                    return false;
                try
                {
                    SendMessage();

                    while (receiveQueue.Count > 0)
                        ReceiveMessage();
                    TimeoutMessage();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error in DHT main loop:");
                    Debug.WriteLine(ex);
                }

                return !engine.Disposed;
            });
        }

        async void MessageReceived(byte[] buffer, IPEndPoint endpoint)
        {
            await DhtEngine.MainLoop;

            lock (locker)
            {
                // I should check the IP address matches as well as the transaction id
                // FIXME: This should throw an exception if the message doesn't exist, we need to handle this
                // and return an error message (if that's what the spec allows)
                try
                {
                    Message message;
                    if (MessageFactory.TryDecodeMessage((BEncodedDictionary)BEncodedValue.Decode(buffer, 0, buffer.Length, false), out message))
                        receiveQueue.Enqueue(new KeyValuePair<IPEndPoint, Message>(endpoint, message));
                }
                catch (MessageException ex)
                {
                    // Caused by bad transaction id usually - ignore
                }
                catch (Exception ex)
                {
                    //throw new Exception("IP:" + endpoint.Address.ToString() + "bad transaction:" + e.Message);
                }
            }
        }

        private void RaiseMessageSent(IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
        {
            //Console.WriteLine ("Query: {0}. Response: {1}. TimedOut: {2}", query.GetType ().Name, response?.GetType ().Name, response == null);
            QuerySent?.Invoke (this, new SendQueryEventArgs(endpoint, query, response));
        }

        private void SendMessage()
        {
            while (CanSend) {
                var details = sendQueue.Dequeue();

                lastSent = details.SentAt = DateTime.UtcNow;
                if (details.Message is QueryMessage)
                    waitingResponse.Add(details.Message.TransactionId, details);

                byte[] buffer = details.Message.Encode();
                listener.Send(buffer, details.Destination);
            }
        }

        internal void Start()
        {
            if (listener.Status != ListenerStatus.Listening)
                listener.Start();
        }

        internal void Stop()
        {
            if (listener.Status != ListenerStatus.NotListening)
                listener.Stop();
        }

        private void TimeoutMessage()
        {
            foreach (var v in waitingResponse) {
                if ((DateTime.UtcNow - v.Value.SentAt) > engine.Timeout)
                    waitingResponseTimedOut.Add (v.Value);
            }

            foreach (var v in waitingResponseTimedOut) {
                MessageFactory.UnregisterSend((QueryMessage)v.Message);
                waitingResponse.Remove (v.Message.TransactionId);

                if (v.CompletionSource != null)
                    v.CompletionSource.TrySetResult (new SendQueryEventArgs (v.Destination, (QueryMessage)v.Message, null));
                RaiseMessageSent (v.Destination, (QueryMessage)v.Message, null);
            }

            waitingResponseTimedOut.Clear ();
        }

        private void ReceiveMessage()
        {
            KeyValuePair<IPEndPoint, Message> receive = receiveQueue.Dequeue();
            Message message = receive.Value;
            ResponseMessage response = message as ResponseMessage;
            IPEndPoint source = receive.Key;
            SendDetails query = default;

            try
            {
                Node node = engine.RoutingTable.FindNode(message.Id);
                if (node == null) {
                    node = new Node(message.Id, source);
                    engine.RoutingTable.Add(node);
                }

                // If we have received a ResponseMessage corresponding to a query we sent, we should
                // remove it from our list before handling it as that could cause an exception to be
                // thrown.
                if (response != null) {
                    query = waitingResponse [response.TransactionId];
                    waitingResponse.Remove (response.TransactionId);
                }

                node.Seen();
                message.Handle(engine, node);
                if (response != null) {
                    if (query.CompletionSource != null)
                        query.CompletionSource.TrySetResult (new SendQueryEventArgs (node.EndPoint, response.Query, response));
                    RaiseMessageSent (node.EndPoint, response.Query, response);
                }
            }
            catch (MessageException ex)
            {
                if (query.CompletionSource != null)
                    query.CompletionSource.TrySetResult (new SendQueryEventArgs (query.Destination, (QueryMessage)query.Message, null));
                // Normal operation (FIXME: do i need to send a response error message?) 
            }
            catch (Exception ex)
            {
                if (query.CompletionSource != null)
                    query.CompletionSource.TrySetResult (new SendQueryEventArgs (query.Destination, (QueryMessage)query.Message, null));
                this.EnqueueSend(new ErrorMessage(ErrorCode.GenericError, "Misshandle received message!"), source);
            }
        }

        internal void EnqueueSend(Message message, IPEndPoint endpoint, System.Threading.Tasks.TaskCompletionSource<SendQueryEventArgs> tcs = null)
        {
            lock (locker)
            {
                if (message.TransactionId == null)
                {
                    if (message is ResponseMessage)
                        throw new ArgumentException("Message must have a transaction id");
                    do {
                        message.TransactionId = TransactionId.NextId();
                    } while (MessageFactory.IsRegistered(message.TransactionId));
                }

                // We need to be able to cancel a query message if we time out waiting for a response
                if (message is QueryMessage)
                    MessageFactory.RegisterSend((QueryMessage)message);

                sendQueue.Enqueue(new SendDetails(endpoint, message, tcs));
            }
        }

        internal void EnqueueSend(Message message, Node node, System.Threading.Tasks.TaskCompletionSource<SendQueryEventArgs> tcs = null)
        {
            EnqueueSend (message, node.EndPoint, tcs);
        }

        public System.Threading.Tasks.Task<SendQueryEventArgs> SendAsync (Message message, Node node)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<SendQueryEventArgs> ();
            EnqueueSend (message, node, tcs);
            return tcs.Task;
        }
    }
}
