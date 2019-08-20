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
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    class MessageLoop
    {
        private struct SendDetails
        {
            public SendDetails(Node node, IPEndPoint destination, DhtMessage message, TaskCompletionSource<SendQueryEventArgs> tcs)
            {
                CompletionSource = tcs;
                Destination = destination;
                Node = node;
                Message = message;
                SentAt = null;
            }
            public TaskCompletionSource<SendQueryEventArgs> CompletionSource;
            public IPEndPoint Destination;
            public DhtMessage Message;
            public Node Node;
            public Stopwatch SentAt;
        }

        internal event Action<object, SendQueryEventArgs> QuerySent;

        DhtEngine engine;
        IDhtListener listener;
        private object locker = new object();
        Queue<SendDetails> sendQueue = new Queue<SendDetails>();
        Queue<KeyValuePair<IPEndPoint, DhtMessage>> receiveQueue = new Queue<KeyValuePair<IPEndPoint, DhtMessage>>();
        Dictionary<BEncodedValue, SendDetails> waitingResponse = new Dictionary<BEncodedValue, SendDetails>();
        List<SendDetails> waitingResponseTimedOut = new List<SendDetails> ();

        internal int PendingQueries
            => waitingResponse.Count;

       internal TimeSpan Timeout { get; set; }

        public MessageLoop(DhtEngine engine, IDhtListener listener)
        {
            this.engine = engine;
            this.listener = listener;
            Timeout = TimeSpan.FromSeconds(15);

            listener.MessageReceived += MessageReceived;
            Task sendTask = null;
            DhtEngine.MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(5), () => {
                if (engine.Disposed)
                    return false;
                try
                {
                    if (sendTask == null || sendTask.IsCompleted)
                        sendTask = SendMessages();

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
                    DhtMessage message;
                    if (DhtMessageFactory.TryDecodeMessage((BEncodedDictionary)BEncodedValue.Decode(buffer, 0, buffer.Length, false), out message))
                        receiveQueue.Enqueue(new KeyValuePair<IPEndPoint, DhtMessage>(endpoint, message));
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

        void RaiseMessageSent(Node node, IPEndPoint endpoint, QueryMessage query)
            => QuerySent?.Invoke (this, new SendQueryEventArgs(node, endpoint, query));

        void RaiseMessageSent(Node node, IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
            => QuerySent?.Invoke (this, new SendQueryEventArgs(node, endpoint, query, response));

        void RaiseMessageSent(Node node, IPEndPoint endpoint, QueryMessage query, ErrorMessage error)
            => QuerySent?.Invoke (this, new SendQueryEventArgs(node, endpoint, query, error));

        private async Task SendMessages()
        {
            for (int i = 0; i < 5 && sendQueue.Count > 0; i ++) {
                var details = sendQueue.Dequeue();

                details.SentAt = Stopwatch.StartNew();
                if (details.Message is QueryMessage)
                    waitingResponse.Add(details.Message.TransactionId, details);

                byte[] buffer = details.Message.Encode();
                await listener.SendAsync(buffer, details.Destination);
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
                if (Timeout == TimeSpan.Zero || v.Value.SentAt.Elapsed > Timeout)
                    waitingResponseTimedOut.Add (v.Value);
            }

            foreach (var v in waitingResponseTimedOut) {
                DhtMessageFactory.UnregisterSend((QueryMessage)v.Message);
                waitingResponse.Remove (v.Message.TransactionId);

                if (v.CompletionSource != null)
                    v.CompletionSource.TrySetResult (new SendQueryEventArgs (v.Node, v.Destination, (QueryMessage)v.Message));
                RaiseMessageSent (v.Node, v.Destination, (QueryMessage)v.Message);
            }

            waitingResponseTimedOut.Clear ();
        }

        private void ReceiveMessage()
        {
            KeyValuePair<IPEndPoint, DhtMessage> receive = receiveQueue.Dequeue();
            DhtMessage message = receive.Value;
            IPEndPoint source = receive.Key;
            SendDetails query = default (SendDetails);

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
                if (message is ResponseMessage || message is ErrorMessage) {
                    query = waitingResponse [message.TransactionId];
                    waitingResponse.Remove (message.TransactionId);
                }

                node.Seen();
                if (message is ResponseMessage response) {
                    response.Handle(engine, node);

                    if (query.CompletionSource != null)
                        query.CompletionSource.TrySetResult (new SendQueryEventArgs (node, node.EndPoint, (QueryMessage) query.Message, response));
                    RaiseMessageSent (node, node.EndPoint, (QueryMessage) query.Message, response);
                } else if (message is ErrorMessage error) {
                    if (query.CompletionSource != null)
                        query.CompletionSource.TrySetResult (new SendQueryEventArgs (node, node.EndPoint, (QueryMessage) query.Message, error));
                    RaiseMessageSent (node, node.EndPoint, (QueryMessage) query.Message, error);
                }
            }
            catch (MessageException)
            {
                var error = new ErrorMessage(message.TransactionId, ErrorCode.GenericError, "Unexpected error responding to the message");
                if (query.CompletionSource != null)
                    query.CompletionSource.TrySetResult (new SendQueryEventArgs (query.Node, query.Destination, (QueryMessage)query.Message, error));
            }
            catch (Exception)
            {
                var error = new ErrorMessage(message.TransactionId, ErrorCode.GenericError, "Unexpected exception responding to the message");
                if (query.CompletionSource != null)
                    query.CompletionSource.TrySetResult (new SendQueryEventArgs (query.Node, query.Destination, (QueryMessage)query.Message, error));
                EnqueueSend(error, null, source);
            }
        }

        internal void EnqueueSend(DhtMessage message, Node node, IPEndPoint endpoint, TaskCompletionSource<SendQueryEventArgs> tcs = null)
        {
            lock (locker)
            {
                if (message.TransactionId == null)
                {
                    if (message is ResponseMessage)
                        throw new ArgumentException("Message must have a transaction id");
                    do {
                        message.TransactionId = TransactionId.NextId();
                    } while (DhtMessageFactory.IsRegistered(message.TransactionId));
                }

                // We need to be able to cancel a query message if we time out waiting for a response
                if (message is QueryMessage)
                    DhtMessageFactory.RegisterSend((QueryMessage)message);

                sendQueue.Enqueue(new SendDetails(node, endpoint, message, tcs));
            }
        }

        internal void EnqueueSend(DhtMessage message, Node node, TaskCompletionSource<SendQueryEventArgs> tcs = null)
        {
            EnqueueSend (message, node, node.EndPoint, tcs);
        }

        public Task<SendQueryEventArgs> SendAsync (DhtMessage message, Node node)
        {
            var tcs = new TaskCompletionSource<SendQueryEventArgs> ();
            EnqueueSend (message, node, tcs);
            return tcs.Task;
        }
    }
}
