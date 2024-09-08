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
using MonoTorrent.Connections;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Logging;

using ReusableTasks;

namespace MonoTorrent.Dht
{
    class MessageLoop
    {
        static readonly ILogger Logger = LoggerFactory.Create (nameof (MessageLoop));

        struct SendDetails
        {
            public SendDetails (Node? node, IPEndPoint destination, DhtMessage message, TaskCompletionSource<SendQueryEventArgs>? tcs)
            {
                CompletionSource = tcs;
                Destination = destination;
                Node = node;
                Message = message;
                SentAt = new ValueStopwatch ();
            }
            public readonly TaskCompletionSource<SendQueryEventArgs>? CompletionSource;
            public readonly IPEndPoint Destination;
            public readonly DhtMessage Message;
            public readonly Node? Node;
            public ValueStopwatch SentAt;
        }

        internal event Action<object, SendQueryEventArgs>? QuerySent;

        internal DhtMessageFactory DhtMessageFactory { get; private set; }

        /// <summary>
        ///  The DHT engine which owns this message loop.
        /// </summary>
        DhtEngine Engine { get; }

        /// <summary>
        /// The listener instance which is used to send/receive messages.
        /// </summary>
        IDhtListener Listener { get; set; }

        TransferMonitor Monitor { get; }

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

        public MessageLoop (DhtEngine engine, TransferMonitor monitor)
        {
            Engine = engine ?? throw new ArgumentNullException (nameof (engine));
            Monitor = monitor;
            DhtMessageFactory = new DhtMessageFactory ();
            Listener = new NullDhtListener ();
            ReceiveQueue = new Queue<KeyValuePair<IPEndPoint, DhtMessage>> ();
            SendQueue = new Queue<SendDetails> ();
            Timeout = TimeSpan.FromSeconds (15);
            WaitingResponse = new Dictionary<BEncodedValue, SendDetails> ();
            WaitingResponseTimedOut = new List<SendDetails> ();

            Task? sendTask = null;
            DhtEngine.MainLoop.QueueTimeout (TimeSpan.FromMilliseconds (5), () => {
                monitor.ReceiveMonitor.Tick ();
                monitor.SendMonitor.Tick ();

                if (engine.Disposed)
                    return false;
                try {
                    if (sendTask == null || sendTask.IsCompleted)
                        sendTask = SendMessages ();

                    while (ReceiveQueue.Count > 0)
                        ReceiveMessage ();

                    TimeoutMessages ();
                } catch (Exception ex) {
                    Debug.WriteLine ("Error in DHT main loop:");
                    Debug.WriteLine (ex);
                }

                return !engine.Disposed;
            });
        }

        async void MessageReceived (ReadOnlyMemory<byte> buffer, IPEndPoint endpoint)
        {
            await DhtEngine.MainLoop;

            // Don't handle new messages if we have already stopped the dht engine.
            if (Listener.Status == ListenerStatus.NotListening)
                return;

            // I should check the IP address matches as well as the transaction id
            // FIXME: This should throw an exception if the message doesn't exist, we need to handle this
            // and return an error message (if that's what the spec allows)
            try {
                if (DhtMessageFactory.TryDecodeMessage ((BEncodedDictionary) BEncodedValue.Decode (buffer.Span, false), out DhtMessage? message)) {
                    Monitor.ReceiveMonitor.AddDelta (buffer.Length);
                    ReceiveQueue.Enqueue (new KeyValuePair<IPEndPoint, DhtMessage> (endpoint, message!));
                }
            } catch (MessageException) {
                // Caused by bad transaction id usually - ignore
            } catch (Exception) {
                //throw new Exception("IP:" + endpoint.Address.ToString() + "bad transaction:" + e.Message);
            }
        }

        void RaiseMessageSent (Node node, IPEndPoint endpoint, QueryMessage query)
        {
            QuerySent?.Invoke (this, new SendQueryEventArgs (node, endpoint, query));
        }

        void RaiseMessageSent (Node node, IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
        {
            QuerySent?.Invoke (this, new SendQueryEventArgs (node, endpoint, query, response));
        }

        void RaiseMessageSent (Node node, IPEndPoint endpoint, QueryMessage query, ErrorMessage error)
        {
            QuerySent?.Invoke (this, new SendQueryEventArgs (node, endpoint, query, error));
        }

        async Task SendMessages ()
        {
            for (int i = 0; i < 5 && SendQueue.Count > 0; i++) {
                SendDetails details = SendQueue.Dequeue ();

                details.SentAt = ValueStopwatch.StartNew ();
                if (details.Message is QueryMessage) {
                    if (details.Message.TransactionId is null) {
                        Logger.Error ("Transaction id was unexpectedly missing while sending messages");
                        return;
                    }
                    WaitingResponse.Add (details.Message.TransactionId, details);
                }

                ReadOnlyMemory<byte> buffer = details.Message.Encode ();
                try {
                    Monitor.SendMonitor.AddDelta (buffer.Length);
                    await Listener.SendAsync (buffer, details.Destination);
                } catch {
                    TimeoutMessage (details);
                }
            }
        }

        internal void Start ()
        {
            DhtEngine.MainLoop.CheckThread ();

            DhtMessageFactory = new DhtMessageFactory ();
            if (Listener.Status != ListenerStatus.Listening)
                Listener.Start ();
        }

        internal void Stop ()
        {
            DhtEngine.MainLoop.CheckThread ();

            DhtMessageFactory = new DhtMessageFactory ();
            SendQueue.Clear ();
            ReceiveQueue.Clear ();
            WaitingResponse.Clear ();
            WaitingResponseTimedOut.Clear ();

            if (Listener.Status != ListenerStatus.NotListening)
                Listener.Stop ();
        }

        void TimeoutMessages ()
        {
            DhtEngine.MainLoop.CheckThread ();

            foreach (KeyValuePair<BEncodedValue, SendDetails> v in WaitingResponse) {
                if (Timeout == TimeSpan.Zero || v.Value.SentAt.Elapsed > Timeout)
                    WaitingResponseTimedOut.Add (v.Value);
            }

            foreach (SendDetails v in WaitingResponseTimedOut)
                TimeoutMessage (v);

            WaitingResponseTimedOut.Clear ();
        }

        void TimeoutMessage (SendDetails v)
        {
            DhtEngine.MainLoop.CheckThread ();

            DhtMessageFactory.UnregisterSend ((QueryMessage) v.Message);
            WaitingResponse.Remove (v.Message.TransactionId!);

            v.CompletionSource?.TrySetResult (new SendQueryEventArgs (v.Node!, v.Destination, (QueryMessage) v.Message));
            RaiseMessageSent (v.Node!, v.Destination, (QueryMessage) v.Message);
        }

        void ReceiveMessage ()
        {
            DhtEngine.MainLoop.CheckThread ();

            KeyValuePair<IPEndPoint, DhtMessage> receive = ReceiveQueue.Dequeue ();
            DhtMessage rawResponse = receive.Value;
            IPEndPoint source = receive.Key;
            SendDetails query = default;

            // What to do if the transaction id is empty?
            BEncodedValue? responseTransactionId = rawResponse.TransactionId;
            if (responseTransactionId is null) {
                Logger.Error ("Received a Dht response with no transaction id");
                return;
            }

            try {
                Node? node = Engine.RoutingTable.FindNode (rawResponse.Id);
                if (node == null) {
                    node = new Node (rawResponse.Id, source);
                    Engine.RoutingTable.Add (node);
                }

                // If we have received a ResponseMessage corresponding to a query we sent, we should
                // remove it from our list before handling it as that could cause an exception to be
                // thrown.
                if (rawResponse is ResponseMessage || rawResponse is ErrorMessage) {
                    if (!WaitingResponse.TryGetValue (responseTransactionId, out query))
                        return;
                    WaitingResponse.Remove (responseTransactionId);
                }

                node.Seen ();
                if (rawResponse is ResponseMessage response) {
                    QueryMessage? queryMessage = query.Message as QueryMessage;
                    if (queryMessage is null) {
                        Logger.Error ("Received a dht response but the corresponding query message was missing");
                        return;
                    }

                    response.Handle (Engine, node);
                    query.CompletionSource?.TrySetResult (new SendQueryEventArgs (node, node.EndPoint, queryMessage, response));
                    RaiseMessageSent (node, node.EndPoint, queryMessage, response);
                } else if (rawResponse is ErrorMessage error) {
                    QueryMessage? queryMessage = query.Message as QueryMessage;
                    if (queryMessage is null) {
                        Logger.Error ("Received a dht response but the corresponding query message was missing");
                        return;
                    }

                    query.CompletionSource?.TrySetResult (new SendQueryEventArgs (node, node.EndPoint, queryMessage, error));
                    RaiseMessageSent (node, node.EndPoint, queryMessage, error);
                }
            } catch (MessageException) {
                // FIXME: Is this the right thing to do?
                // Can/should we attempt to send a response if an error occurs here? Do we have valid data for the node?
                var error = new ErrorMessage (responseTransactionId, ErrorCode.GenericError, "Unexpected error responding to the message");
                query.CompletionSource?.TrySetResult (new SendQueryEventArgs (query.Node!, query.Destination!, (QueryMessage) query.Message!, error));
                EnqueueSend (error, null, source);
            } catch (Exception) {
                // FIXME: Is this the right thing to do?
                // Can/should we attempt to send a response if an error occurs here? Do we have valid data for the node?
                var error = new ErrorMessage (responseTransactionId, ErrorCode.GenericError, "Unexpected exception responding to the message");
                query.CompletionSource?.TrySetResult (new SendQueryEventArgs (query.Node!, query.Destination!, (QueryMessage) query.Message!, error));
                EnqueueSend (error, null, source);
            }
        }

        internal ReusableTask SetListener (IDhtListener listener)
        {
            DhtEngine.MainLoop.CheckThread ();

            Listener.MessageReceived -= MessageReceived;
            Listener = listener ?? new NullDhtListener ();
            Listener.MessageReceived += MessageReceived;
            return ReusableTask.CompletedTask;
        }

        internal void EnqueueSend (DhtMessage message, Node? node, IPEndPoint endpoint, TaskCompletionSource<SendQueryEventArgs>? tcs = null)
        {
            DhtEngine.MainLoop.CheckThread ();

            if (message.TransactionId == null) {
                if (message is ResponseMessage)
                    throw new ArgumentException ("Message must have a transaction id");
                do {
                    message.TransactionId = TransactionId.NextId ();
                } while (DhtMessageFactory.IsRegistered (message.TransactionId));
            }

            // We need to be able to cancel a query message if we time out waiting for a response
            if (message is QueryMessage)
                DhtMessageFactory.RegisterSend ((QueryMessage) message);

            SendQueue.Enqueue (new SendDetails (node, endpoint, message, tcs));
        }

        internal void EnqueueSend (DhtMessage message, Node node, TaskCompletionSource<SendQueryEventArgs>? tcs = null)
        {
            DhtEngine.MainLoop.CheckThread ();

            EnqueueSend (message, node, node.EndPoint, tcs);
        }

        public Task<SendQueryEventArgs> SendAsync (DhtMessage message, Node node)
        {
            DhtEngine.MainLoop.CheckThread ();

            var tcs = new TaskCompletionSource<SendQueryEventArgs> ();
            EnqueueSend (message, node, tcs);
            return tcs.Task;
        }
    }
}
