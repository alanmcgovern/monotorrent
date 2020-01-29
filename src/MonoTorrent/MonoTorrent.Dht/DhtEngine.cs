//
// DhtEngine.cs
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
using System.Net;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Dht.Tasks;

namespace MonoTorrent.Dht
{
    enum ErrorCode
    {
        GenericError = 201,
        ServerError = 202,
        ProtocolError = 203,// malformed packet, invalid arguments, or bad token
        MethodUnknown = 204//Method Unknown
    }

    public class DhtEngine : IDisposable, IDhtEngine
    {
        internal static readonly TimeSpan AnnounceInternal = TimeSpan.FromMinutes (10);
        internal static readonly TimeSpan MinimumAnnounceInterval = TimeSpan.FromMinutes (3);

        #region Events

        public event EventHandler<PeersFoundEventArgs> PeersFound;
        public event EventHandler StateChanged;

        #endregion Events

        #region Fields

        internal static MainLoop MainLoop { get; } = new MainLoop ("DhtLoop");

        #endregion Fields

        #region Properties

        public bool Disposed { get; private set; }
        public DhtState State { get; private set; }

        internal TimeSpan BucketRefreshTimeout { get; set; }
        internal NodeId LocalId => RoutingTable.LocalNode.Id;
        internal MessageLoop MessageLoop { get; }
        internal RoutingTable RoutingTable { get; }
        internal TokenManager TokenManager { get; }
        internal Dictionary<NodeId, List<Node>> Torrents { get; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Creates a new DhtEngine which listens for connections on the given endpoint
        /// </summary>
        /// <param name="listenerEndpoint">The IPAddresss/port which the engine should listen on</param>
        public DhtEngine (IPEndPoint listenerEndpoint)
            : this (new DhtListener (listenerEndpoint))
        {
        }

        public DhtEngine (IDhtListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException (nameof (listener));

            BucketRefreshTimeout = TimeSpan.FromMinutes (15);
            MessageLoop = new MessageLoop (this, listener);
            RoutingTable = new RoutingTable ();
            State = DhtState.NotReady;
            TokenManager = new TokenManager ();
            Torrents = new Dictionary<NodeId, List<Node>> ();

            MainLoop.QueueTimeout (TimeSpan.FromMinutes (5), () => {
                if (!Disposed)
                    TokenManager.RefreshTokens ();
                return !Disposed;
            });
        }

        #endregion Constructors

        #region Methods

        public void Add (BEncodedList nodes)
        {
            // Maybe we should pipeline all our tasks to ensure we don't flood the DHT engine.
            // I don't think it's *bad* that we can run several initialise tasks simultaenously
            // but it might be better to run them sequentially instead. We should also
            // run GetPeers and Announce tasks sequentially.
            var task = new InitialiseTask (this, Node.FromCompactNode (nodes));
            _ = task.ExecuteAsync ();
        }

        internal void Add (IEnumerable<Node> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException (nameof (nodes));

            foreach (Node n in nodes)
                Add (n);
        }

        internal async void Add (Node node)
        {
            if (node == null)
                throw new ArgumentNullException (nameof (node));

            try {
                await MainLoop;
                await SendQueryAsync (new Ping (RoutingTable.LocalNode.Id), node);
            } catch {
                // Ignore?
            }
        }

        public async void Announce (InfoHash infoHash, int port)
        {
            CheckDisposed ();
            Check.InfoHash (infoHash);

            try {
                await MainLoop;
                var task = new AnnounceTask (this, infoHash, port);
                await task.ExecuteAsync ();
            } catch {
                // Ignore?
            }
        }

        void CheckDisposed ()
        {
            if (Disposed)
                throw new ObjectDisposedException (GetType ().Name);
        }

        public void Dispose ()
        {
            if (Disposed)
                return;

            // Ensure we don't break any threads actively running right now
            MainLoop.QueueWait (() => {
                Disposed = true;
            });
        }

        public async void GetPeers (InfoHash infoHash)
        {
            CheckDisposed ();
            Check.InfoHash (infoHash);

            try {
                await MainLoop;
                var task = new GetPeersTask (this, infoHash);
                await task.ExecuteAsync ();
            } catch {
                // Ignore?
            }
        }

        async void InitializeAsync (byte[] initialNodes)
        {
            if (initialNodes == null) {
                initialNodes = Array.Empty<byte> ();
            }

            var initTask = new InitialiseTask (this, Node.FromCompactNode (initialNodes));
            await initTask.ExecuteAsync ();
            RaiseStateChanged (DhtState.Ready);
        }

        internal void RaisePeersFound (NodeId infoHash, IList<Peer> peers)
        {
            PeersFound?.Invoke (this, new PeersFoundEventArgs (new InfoHash (infoHash.Bytes), peers));
        }

        void RaiseStateChanged (DhtState newState)
        {
            State = newState;
            StateChanged?.Invoke (this, EventArgs.Empty);
        }

        internal async Task RefreshBuckets ()
        {
            var refreshTasks = new List<Task> ();
            foreach (Bucket b in RoutingTable.Buckets) {
                if (b.LastChanged > BucketRefreshTimeout) {
                    b.Changed ();
                    var task = new RefreshBucketTask (this, b);
                    refreshTasks.Add (task.Execute ());
                }
            }

            if (refreshTasks.Count > 0)
                await Task.WhenAll (refreshTasks).ConfigureAwait (false);
        }

        public async Task<byte[]> SaveNodesAsync ()
        {
            await MainLoop;

            var details = new BEncodedList ();

            foreach (Bucket b in RoutingTable.Buckets) {
                foreach (Node n in b.Nodes)
                    if (n.State != NodeState.Bad)
                        details.Add (n.CompactNode ());

                if (b.Replacement != null)
                    if (b.Replacement.State != NodeState.Bad)
                        details.Add (b.Replacement.CompactNode ());
            }

            return details.Encode ();
        }

        internal async Task<SendQueryEventArgs> SendQueryAsync (QueryMessage query, Node node)
        {
            var e = default (SendQueryEventArgs);
            for (int i = 0; i < 4; i++) {
                e = await MessageLoop.SendAsync (query, node);

                // If the message timed out and we we haven't already hit the maximum retries
                // send again. Otherwise we propagate the eventargs through the Complete event.
                if (e.TimedOut) {
                    node.FailedCount++;
                    continue;
                } else {
                    node.Seen ();
                    return e;
                }
            }

            return e;
        }

        public async Task StartAsync ()
        {
            await StartAsync (Array.Empty<byte> ());
        }

        public async Task StartAsync (byte[] initialNodes)
        {
            CheckDisposed ();

            await MainLoop;
            MessageLoop.Start ();
            if (RoutingTable.NeedsBootstrap) {
                RaiseStateChanged (DhtState.Initialising);
                InitializeAsync (initialNodes);
            } else {
                RaiseStateChanged (DhtState.Ready);
            }

            MainLoop.QueueTimeout (TimeSpan.FromSeconds (30), delegate {
                if (!Disposed) {
                    _ = RefreshBuckets ();
                }
                return !Disposed;
            });
        }

        public async Task StopAsync ()
        {
            await MainLoop;

            MessageLoop.Stop ();
            RaiseStateChanged (DhtState.NotReady);
        }

        internal async Task WaitForState (DhtState state)
        {
            await MainLoop;
            if (State == state)
                return;

            var tcs = new TaskCompletionSource<object> ();

            void handler (object o, EventArgs e)
            {
                if (State == state) {
                    StateChanged -= handler;
                    tcs.SetResult (true);
                }
            }

            StateChanged += handler;
            await tcs.Task;
        }
        #endregion Methods
    }
}
