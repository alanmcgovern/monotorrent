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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Dht.Messages.Efficient;
using MonoTorrent.Logging;

using ReusableTasks;

namespace MonoTorrent.Dht
{
    class MessageLoop
    {
        static readonly ILogger Logger = LoggerFactory.Create (nameof (MessageLoop));

        struct SendDetails
        {
            public SendDetails (Node? node, CompactEndPoint destination, ReadOnlyMemory<byte> message, TaskCompletionSource<SendQueryEventArgs>? tcs)
            {
                CompletionSource = tcs;
                Destination = destination;
                Node = node;
                Message = message;
                SentAt = new ValueStopwatch ();
            }
            public readonly TaskCompletionSource<SendQueryEventArgs>? CompletionSource;
            public readonly CompactEndPoint Destination;
            public readonly ReadOnlyMemory<byte> Message;
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
        Queue<KeyValuePair<CompactEndPoint, ReadOnlyMemory<byte>>> ReceiveQueue { get; }

        Memory<byte> SendBuffer { get; set; }

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
        Dictionary<(TransactionId, CompactEndPoint), SendDetails> WaitingResponse { get; }

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
            ReceiveQueue = new Queue<KeyValuePair<CompactEndPoint, ReadOnlyMemory<byte>>> ();
            SendQueue = new Queue<SendDetails> ();
            Timeout = TimeSpan.FromSeconds (10);
            WaitingResponse = new Dictionary<(TransactionId, CompactEndPoint), SendDetails> ();
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

        async void MessageReceived (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
        {
            await DhtEngine.MainLoop;

            // Don't handle new messages if we have already stopped the dht engine.
            if (Listener.Status == ListenerStatus.NotListening)
                return;

            Monitor.ReceiveMonitor.AddDelta (buffer.Length);

            try {
                //Console.WriteLine ("R: " + Encoding.UTF8.GetString (buffer.Span));

                if (buffer.Span.Length > 0 && buffer.Span[0] != (byte) 'd') {
                    // Definitely cannot be a BEncoded Dictionary. Drop it immediately.
                    return;
                }

                var m = KrpcMessage.Parse (buffer);
                if (m.MessageType == KrpcType.Response || m.MessageType == KrpcType.Error) {
                    // If we can' unregister the corresponding query then this response message should be ignored.
                    if (!DhtMessageFactory.UnregisterSend (m.TransactionId, endpoint))
                        return;
                }
                ReceiveQueue.Enqueue (new KeyValuePair<CompactEndPoint, ReadOnlyMemory<byte>> (endpoint, buffer));
            } catch (MessageException) {
                // Caused by bad transaction id usually - ignore
            } catch (Exception) {
                //throw new Exception("IP:" + endpoint.Address.ToString() + "bad transaction:" + e.Message);
            }
        }

        void RaiseMessageSent (Node node, CompactEndPoint endpoint, ReadOnlyMemory<byte> query)
        {
            QuerySent?.Invoke (this, new SendQueryEventArgs (node, endpoint, query));
        }

        void RaiseMessageSent (Node node, CompactEndPoint endpoint, ReadOnlyMemory<byte> query, ReadOnlyMemory<byte> response)
        {
            QuerySent?.Invoke (this, new SendQueryEventArgs (node, endpoint, query, response));
        }

        async Task SendMessages ()
        {
            for (int i = 0; i < 50 && SendQueue.Count > 0; i++) {
                SendDetails details = SendQueue.Dequeue ();

                details.SentAt = ValueStopwatch.StartNew ();
                var message = KrpcMessage.Parse (details.Message);
                // FIXME: Don't merge message type (query, response, error) with the actual undetlying message (getpeers, findnode, ping etc)
                if (message.MessageType == KrpcType.Query) {
                    if (message.TransactionId.IsEmpty) {
                        Logger.Error ("Transaction id was unexpectedly missing while sending messages");
                        return;
                    }
                    WaitingResponse.Add ((TransactionId.From (message.TransactionId), details.Destination), details);
                }
                
                try {
                    Monitor.SendMonitor.AddDelta (details.Message.Length);
                    //Console.WriteLine ("S: " + Encoding.UTF8.GetString (details.Message.Span));
                    await Listener.SendAsync (details.Message, details.Destination);
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

            foreach (KeyValuePair<(TransactionId, CompactEndPoint), SendDetails> v in WaitingResponse) {
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

            var m = KrpcMessage.Parse (v.Message);
            DhtMessageFactory.UnregisterSend (m.TransactionId, v.Destination);
            WaitingResponse.Remove ((TransactionId.From (m.TransactionId), v.Destination));

            v.CompletionSource?.TrySetResult (new SendQueryEventArgs (v.Node!, v.Destination, v.Message));
            RaiseMessageSent (v.Node!, v.Destination, v.Message);
        }

        void ReceiveMessage ()
        {
            DhtEngine.MainLoop.CheckThread ();

            KeyValuePair<CompactEndPoint, ReadOnlyMemory<byte>> receive = ReceiveQueue.Dequeue ();
            var rawResponse = KrpcMessage.Parse(receive.Value);
            CompactEndPoint source = receive.Key;
            SendDetails query = default;

            // What to do if the transaction id is empty?
            if (rawResponse.TransactionId.IsEmpty) {
                Logger.Error ("Received a Dht message with no transaction id");
                return;
            }

            try {
                // If we have received a ResponseMessage corresponding to a query we sent, we should
                // remove it from our list before handling it as that could cause an exception to be
                // thrown.
                if (rawResponse.MessageType == KrpcType.Response || rawResponse.MessageType == KrpcType.Error) {
                    if (!WaitingResponse.Remove ((TransactionId.From (rawResponse.TransactionId), source), out query))
                        return;
                }

                if (rawResponse.MessageType == KrpcType.Error) {
                    HandleError (query, source, receive.Value);
                    return;
                }

                // Requests and responses need to have the nodeid of the queried node.
                if (rawResponse.NodeId.IsEmpty) {
                    Logger.Error ("Received a request or response which didn't contain a node id");
                    return;
                }
                
                Node? node = Engine.RoutingTable.FindNode (new NodeId (rawResponse.NodeId));
                if (node == null) {
                    node = new Node (new NodeId (rawResponse.NodeId), source);
                    Engine.RoutingTable.Add (node);
                }
                node.Seen ();

                if (rawResponse.MessageType == KrpcType.Response) {
                    HandleResponse (query, node, ref rawResponse, receive.Value);
                } else {
                    switch (rawResponse.QueryMethod) {
                        case QueryMethod.AnnouncePeer:
                            HandleAnnouncePeer (node, ref rawResponse);
                            break;
                        case QueryMethod.FindNode:
                            HandleFindNode (node, ref rawResponse);
                            break;
                        case QueryMethod.GetPeers:
                            HandleGetPeers (node, ref rawResponse);
                            break;
                        case QueryMethod.Ping:
                            HandlePing (node, ref rawResponse);
                            break;
                        default:
                            throw new NotSupportedException ("what is this?");
                    }
                }
            } catch (MessageException) {
                // FIXME: Is this the right thing to do?
                // Can/should we attempt to send a response if an error occurs here? Do we have valid data for the node?
                //var error = new ErrorMessage (BitConverter.ToUInt16(rawResponse.TransactionId), ErrorCode.GenericError, "Unexpected error responding to the message");
                //query.CompletionSource?.TrySetResult (new SendQueryEventArgs (query.Node!, query.Destination!, (QueryMessage) query.Message!, error));
                //EnqueueSend (error, null, source);
            } catch (Exception) {
                // FIXME: Is this the right thing to do?
                // Can/should we attempt to send a response if an error occurs here? Do we have valid data for the node?
                //var error = new ErrorMessage (BitConverter.ToUInt16(rawResponse.TransactionId), ErrorCode.GenericError, "Unexpected exception responding to the message");
                //query.CompletionSource?.TrySetResult (new SendQueryEventArgs (query.Node!, query.Destination!, (QueryMessage) query.Message!, error));
                //EnqueueSend (error, null, source);
            }
        }

        void HandleAnnouncePeer (Node node, ref KrpcMessage rawResponse)
        {
            ReadOnlyMemory<byte> response;
            if (Engine.TokenManager.VerifyToken (node, rawResponse.Request.Token)) {
                var infoHash = new NodeId (rawResponse.Request.InfoHash);
                if (!Engine.Torrents.ContainsKey (infoHash))
                    Engine.Torrents.Add (infoHash, new List<Node> ());
                Engine.Torrents[infoHash].Add (node);
                response = KrpcMessageEncoder.EncodeAnnouncePeerResponse (rawResponse.TransactionId, Engine.LocalId);
            } else {
                response = KrpcMessageEncoder.EncodeError (rawResponse.TransactionId, (int) ErrorCode.ProtocolError, "Invalid or expired token received"u8);
            }
            Engine.MessageLoop.EnqueueSend (response, node, node.EndPoint);
        }

        void HandleFindNode (Node node, ref KrpcMessage rawResponse)
        {
            var nodeId = new NodeId (rawResponse.Request.Target);
            Node? targetNode = Engine.RoutingTable.FindNode (nodeId);
            var nodes = !(targetNode is null)
                ? targetNode.CompactNode ()
                : Node.CompactNode (Engine.RoutingTable.GetClosest (nodeId));

            var response = KrpcMessageEncoder.EncodeFindNodeResponse (rawResponse.TransactionId, Engine.LocalId, nodes.Span);
            Engine.MessageLoop.EnqueueSend (response, node, node.EndPoint);
        }

        void HandleGetPeers (Node node, ref KrpcMessage rawResponse)
        {
            ReadOnlyMemory<byte> response;
            Span<byte> token = stackalloc byte[Engine.TokenManager.TokenLength];
            Engine.TokenManager.TryGenerateToken (node, token, out _);
            var infoHash = new NodeId (rawResponse.Request.InfoHash);
            if (Engine.Torrents.ContainsKey (infoHash)) {
                var list = new BEncodedList ();
                foreach (Node n in Engine.Torrents[infoHash])
                    list.Add (n.CompactEndPoint ());
                response = KrpcMessageEncoder.EncodeGetPeersResponseValues (rawResponse.TransactionId, Engine.LocalId, token, list.Encode ());
            } else {
                response = KrpcMessageEncoder.EncodeGetPeersResponseNodes (rawResponse.TransactionId, Engine.LocalId, token, Node.CompactNode (Engine.RoutingTable.GetClosest (infoHash)).Span);
            }

            Engine.MessageLoop.EnqueueSend (response, node, node.EndPoint);
        }

        void HandlePing (Node node, ref KrpcMessage rawResponse)
        {
            var m = KrpcMessageEncoder.EncodePingResponse (rawResponse.TransactionId, Engine.LocalId);
            Engine.MessageLoop.EnqueueSend (m, node, node.EndPoint);
        }

        void HandleError (SendDetails query, CompactEndPoint source, ReadOnlyMemory<byte> errorResposne)
        {
            if (query.Message.IsEmpty) {
                Logger.Error ("Received a dht response but the corresponding query message was missing");
                return;
            }

            query.CompletionSource?.TrySetResult (new SendQueryEventArgs (query.Node!, source, query.Message, errorResposne));
            RaiseMessageSent (query.Node!, source, query.Message, errorResposne);
        }

        void HandleResponse (SendDetails query, Node node, ref KrpcMessage rawResponse, ReadOnlyMemory<byte> response)
        {
            if (query.Message.IsEmpty) {
                Logger.Error ("Received a dht response but the corresponding query message was missing");
                return;
            }

            node.Seen ();
            if (!rawResponse.Response.Token.IsEmpty)
                node.Token = new BEncodedString (rawResponse.Response.Token.ToArray ());
            query.CompletionSource?.TrySetResult (new SendQueryEventArgs (node, node.EndPoint, query.Message, response));
            RaiseMessageSent (node, node.EndPoint, query.Message, response);
        }

        internal ReusableTask SetListener (IDhtListener listener)
        {
            DhtEngine.MainLoop.CheckThread ();

            Listener.MessageReceived -= MessageReceived;
            Listener = listener ?? new NullDhtListener ();
            Listener.MessageReceived += MessageReceived;
            return ReusableTask.CompletedTask;
        }

        internal void EnqueueSend (ReadOnlyMemory<byte> messageBuffer, Node? node, CompactEndPoint endPoint, TaskCompletionSource<SendQueryEventArgs>? tcs = null)
        {
            DhtEngine.MainLoop.CheckThread ();

            var message = KrpcMessage.Parse (messageBuffer);
            if (message.TransactionId.IsEmpty) {
                throw new ArgumentException ("Message must have a transaction id");
            }

            // We need to be able to cancel a query message if we time out waiting for a response
            if (message.MessageType == KrpcType.Query)
                DhtMessageFactory.RegisterSend (message.TransactionId, messageBuffer, endPoint);

            SendQueue.Enqueue (new SendDetails (node, endPoint, messageBuffer, tcs));
        }

        internal void EnqueueSend (ReadOnlyMemory<byte> message, Node node, TaskCompletionSource<SendQueryEventArgs>? tcs = null)
        {
            DhtEngine.MainLoop.CheckThread ();

            EnqueueSend (message, node, node.EndPoint, tcs);
        }

        public Task<SendQueryEventArgs> SendAsync (ReadOnlyMemory<byte> message, Node node)
        {
            DhtEngine.MainLoop.CheckThread ();

            var tcs = new TaskCompletionSource<SendQueryEventArgs> ();
            EnqueueSend (message, node, tcs);
            return tcs.Task;
        }
    }
}
