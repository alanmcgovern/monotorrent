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
using System.Net;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

using MonoTorrent;
using MonoTorrent.Common;
using MonoTorrent.Client;
using MonoTorrent.BEncoding;
using System.IO;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Client.Messages;
using MonoTorrent.Dht.Tasks;

namespace MonoTorrent.Dht
{
    internal enum ErrorCode : int
    {
        GenericError = 201,
        ServerError = 202,
        ProtocolError = 203,// malformed packet, invalid arguments, or bad token
        MethodUnknown = 204//Method Unknown
    }

    public class DhtEngine : IDisposable
    {
        #region Events

        public event EventHandler<PeersFoundEventArgs> PeersFound;
        public event EventHandler StateChanged;

        #endregion Events

        #region Fields

        internal static MainLoop MainLoop = new MainLoop("DhtLoop");

        bool bootStrap = true;
        TimeSpan bucketRefreshTimeout = TimeSpan.FromMinutes(15);
        bool disposed;
        MessageLoop messageLoop;
        int port = 6881;
        State state = State.NotReady;
        RoutingTable table = new RoutingTable();
        TimeSpan timeout;
        Dictionary<NodeId, List<Node>> torrents = new Dictionary<NodeId, List<Node>>();
        TokenManager tokenManager;

        #endregion Fields

        #region Properties

        internal bool Bootstrap
        {
            get { return bootStrap; }
            set { bootStrap = value; }
        }

        internal TimeSpan BucketRefreshTimeout
        {
            get { return bucketRefreshTimeout; }
            set { bucketRefreshTimeout = value; }
        }

        public bool Disposed
        {
            get { return disposed; }
        }

        internal NodeId LocalId
        {
            get { return RoutingTable.LocalNode.Id; }
        }

        internal MessageLoop MessageLoop
        {
            get { return messageLoop; }
        }

        internal RoutingTable RoutingTable
        {
            get { return table; }
        }

        public State State
        {
            get { return state; }
        }

        internal TimeSpan TimeOut
        {
            get { return timeout; }
            set { timeout = value; }
        }

        internal TokenManager TokenManager
        {
            get { return tokenManager; }
        }

        internal Dictionary<NodeId, List<Node>> Torrents
        {
            get { return torrents; }
        }

        #endregion Properties

        #region Constructors

        public DhtEngine(DhtListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            messageLoop = new MessageLoop(this, listener);
            timeout = TimeSpan.FromSeconds(15); // 15 second message timeout by default
            tokenManager = new TokenManager();
        }

        #endregion Constructors

        #region Methods

        internal void Add(IEnumerable<Node> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException("nodes");

            foreach (Node n in nodes)
                Add(n);
        }

        internal void Add(Node node)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            SendQueryTask task = new SendQueryTask(this, new Ping(RoutingTable.LocalNode.Id), node);
            task.Execute();
        }

        public void Announce(byte[] infoHash, int port)
        {
            CheckDisposed();
            if (infoHash == null)
                throw new ArgumentNullException("infoHash");
            if (infoHash.Length != 20)
                throw new ArgumentException("infoHash must be 20 bytes");
            new AnnounceTask(this, infoHash, port).Execute();
        }

        void CheckDisposed()
        {
            if (Disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            disposed = true;
        }

        public void GetPeers(byte[] infoHash)
        {
            CheckDisposed();
            if (infoHash == null)
                throw new ArgumentNullException("infoHash");
            if (infoHash.Length != 20)
                throw new ArgumentException("infoHash must be 20 bytes");
            new GetPeersTask(this, infoHash).Execute();
        }

        public void Initialise()
        {
            CheckDisposed();
            if (Bootstrap)
            {
                new InitialiseTask(this).Execute();
                RaiseStateChanged(State.Initialising);
                bootStrap = false;
            }
            else
            {
                RaiseStateChanged(State.Ready);
            }

            DhtEngine.MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(200), delegate
            {
                if (Disposed)
                    return false;

                foreach (Bucket b in RoutingTable.Buckets)
                {
                    if ((DateTime.UtcNow - b.LastChanged) > BucketRefreshTimeout)
                    {
                        b.LastChanged = DateTime.UtcNow;
                        RefreshBucketTask task = new RefreshBucketTask(this, b);
                        task.Execute();
                    }
                }
                return !Disposed;
            });
        }

        public void LoadNodes(byte[] nodes)
        {
            CheckDisposed();
            MainLoop.QueueWait(delegate
            {
                BEncodedList list = (BEncodedList)BEncodedValue.Decode(nodes);
                foreach (BEncodedString s in list)
                    Add(Node.FromCompactNode(s.TextBytes, 0));
            });
        }

        void RaiseStateChanged(State newState)
        {
            state = newState;

            if (StateChanged != null)
                StateChanged(this, EventArgs.Empty);
        }

        internal void RaisePeersFound(NodeId infoHash, List<Peer> peers)
        {
            if (PeersFound != null)
                PeersFound(this, new PeersFoundEventArgs(infoHash.Bytes, peers));
        }

        public byte[] SaveNodes()
        {
            BEncodedList details = new BEncodedList();

            MainLoop.QueueWait(delegate {
                foreach (Bucket b in RoutingTable.Buckets)
                {
                    foreach (Node n in b.Nodes)
                        if (n.State != NodeState.Bad)
                            details.Add(n.CompactNode());

                    if (b.Replacement != null)
                        if (b.Replacement.State != NodeState.Bad)
                            details.Add(b.Replacement.CompactNode());
                }
            });

            return details.Encode();
        }

        #endregion Methods
    }
}
