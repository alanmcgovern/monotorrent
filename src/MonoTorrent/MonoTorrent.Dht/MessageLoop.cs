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
        struct SendDetails
        {
            public SendDetails(Node node, IPEndPoint destination, DhtMessage message, TaskCompletionSource<SendQueryEventArgs> tcs)
            {
                CompletionSource = tcs;
                Destination = destination;
                Node = node;
                Message = message;
                SentAt = new ValueStopwatch ();
            }
            public TaskCompletionSource<SendQueryEventArgs> CompletionSource;
            public IPEndPoint Destination;
            public DhtMessage Message;
            public Node Node;
            public ValueStopwatch SentAt;
        }

        internal event Action<object, SendQueryEventArgs> QuerySent;

        /// <summary>
        ///  The DHT engine which owns this message loop.
        /// </summary>
        DhtEngine Engine { get; }

        /// <summary>
        /// The listener instance which is used to send/receive messages.
        /// </summary>
        IDhtListener Listener { get; }

        /// <summary>
        /// The number of DHT messages which have been sent and no response has been received.
        /// </summary>
        internal int PendingQueries => WaitingResponse.Count;

        /// <summary>
        /// The list of messages which have been received from the attached IDhtListener which
        /// are waiting to be processed by the engine.
        /// </summary>
        Queue<KeyValuePair<IPEndPoint, DhtMessage>> ReceiveQueue { get; }

        /// <summary>
        /// The list of messages which have been queued to send.
        /// </summary>
        Queue<SendDetails> SendQueue { get; }

        /// <summary>
        /// If a response is not received before the timeout expires, it will be cancelled.
        /// </summary>
        internal TimeSpan Timeout { get; set; }

        /// <summary>
        /// This is the list of messages which have been sent but no response (or error) has
        /// been received yet. The key for the dictionary is the TransactionId for the Query.
        /// </summary>
        Dictionary<BEncodedValue, SendDetails> WaitingResponse { get; }

        /// <summary>
        /// Temporary (re-usable) storage when cancelling timed out messages.
        /// </summary>
        List<SendDetails> WaitingResponseTimedOut { get; }

        public MessageLoop(DhtEngine engine, IDhtListener listener)
        {
            Engine = engine ?? throw new ArgumentNullException (nameof (engine));
            Listener = listener ?? throw new ArgumentNullException (nameof (engine));
            ReceiveQueue = new Queue<KeyValuePair<IPEndPoint, DhtMessage>>();
            SendQueue = new Queue<SendDetails>();
            Timeout = TimeSpan.FromSeconds(15);
            WaitingResponse = new Dictionary<BEncodedValue, SendDetails>();
            WaitingResponseTimedOut = new List<SendDetails> ();

            listener.MessageReceived += MessageReceived;
            Task sendTask = null;
            DhtEngine.MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(5), () => {
                if (engine.Disposed)
                    return false;
                try
                {
                    if (sendTask == null || sendTask.IsCompleted)
                        sendTask = SendMessages();

                    while (ReceiveQueue.Count > 0)
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

            // I should check the IP address matches as well as the transaction id
            // FIXME: This should throw an exception if the message doesn't exist, we need to handle this
            // and return an error message (if that's what the spec allows)
            try
            {
                DhtMessage message;
                if (DhtMessageFactory.TryDecodeMessage((BEncodedDictionary)BEncodedValue.Decode(buffer, 0, buffer.Length, false), out message))
                    ReceiveQueue.Enqueue(new KeyValuePair<IPEndPoint, DhtMessage>(endpoint, message));
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

        void RaiseMessageSent(Node node, IPEndPoint endpoint, QueryMessage query)
            => QuerySent?.Invoke (this, new SendQueryEventArgs(node, endpoint, query));

        void RaiseMessageSent(Node node, IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
            => QuerySent?.Invoke (this, new SendQueryEventArgs(node, endpoint, query, response));

        void RaiseMessageSent(Node node, IPEndPoint endpoint, QueryMessage query, ErrorMessage error)
            => QuerySent?.Invoke (this, new SendQueryEventArgs(node, endpoint, query, error));

        private async Task SendMessages()
        {
            for (int i = 0; i < 5 && SendQueue.Count > 0; i ++) {
                var details = SendQueue.Dequeue();

                details.SentAt = ValueStopwatch.StartNew();
                if (details.Message is QueryMessage)
                    WaitingResponse.Add(details.Message.TransactionId, details);

                byte[] buffer = details.Message.Encode();
                await Listener.SendAsync(buffer, details.Destination);
            }
        }

        internal void Start()
        {
            if (Listener.Status != ListenerStatus.Listening)
                Listener.Start();
        }

        internal void Stop()
        {
            if (Listener.Status != ListenerStatus.NotListening)
                Listener.Stop();
        }

        private void TimeoutMessage()
        {
            foreach (var v in WaitingResponse) {
                if (Timeout == TimeSpan.Zero || v.Value.SentAt.Elapsed > Timeout)
                    WaitingResponseTimedOut.Add (v.Value);
            }

            foreach (var v in WaitingResponseTimedOut) {
                DhtMessageFactory.UnregisterSend((QueryMessage)v.Message);
                WaitingResponse.Remove (v.Message.TransactionId);

                if (v.CompletionSource != null)
                    v.CompletionSource.TrySetResult (new SendQueryEventArgs (v.Node, v.Destination, (QueryMessage)v.Message));
                RaiseMessageSent (v.Node, v.Destination, (QueryMessage)v.Message);
            }

            WaitingResponseTimedOut.Clear ();
        }

        private void ReceiveMessage()
        {
            KeyValuePair<IPEndPoint, DhtMessage> receive = ReceiveQueue.Dequeue();
            DhtMessage message = receive.Value;
            IPEndPoint source = receive.Key;
            SendDetails query = default (SendDetails);

            try
            {
                Node node = Engine.RoutingTable.FindNode(message.Id);
                if (node == null) {
                    node = new Node(message.Id, source);
                    Engine.RoutingTable.Add(node);
                }

                // If we have received a ResponseMessage corresponding to a query we sent, we should
                // remove it from our list before handling it as that could cause an exception to be
                // thrown.
                if (message is ResponseMessage || message is ErrorMessage) {
                    if (!WaitingResponse.TryGetValue (message.TransactionId, out query))
                        return;
                    WaitingResponse.Remove (message.TransactionId);
                }

                node.Seen();
                if (message is ResponseMessage response) {
                    response.Handle(Engine, node);

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

            SendQueue.Enqueue(new SendDetails(node, endpoint, message, tcs));
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
