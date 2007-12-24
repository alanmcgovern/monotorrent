using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    internal enum PeerType
    {
        Connecting,
        Connected,
        Available,
        UploadQueue,
        DownloadQueue,
        Busy,
        Banned
    }

    public class PeerManager
    {
        #region Member Variables

        private ClientEngine engine;
        private TorrentManager manager;
        private List<Peer> availablePeers;
        private List<Peer> banned;
        private List<Peer> busy;
        private List<Peer> connectedPeers;
        private List<Peer> connectingTo;
        #endregion Member Variables


        #region Properties

        /// <summary>
        /// Returns the total number of peers available (including ones already connected to)
        /// </summary>
        public int Available
        {
            get { return this.availablePeers.Count + this.connectedPeers.Count + this.connectingTo.Count; }
        }

        /// <summary>
        /// The list of peers that are available to be connected to
        /// </summary>
        internal List<Peer> AvailablePeers
        {
            get { return this.availablePeers; }
        }

        /// <summary>
        /// The list of peers that we are currently connected to
        /// </summary>
        internal List<Peer> ConnectedPeers
        {
            get { return this.connectedPeers; }
        }

        /// <summary>
        /// The list of peers that we are currently trying to connect to
        /// </summary>
        internal List<Peer> ConnectingToPeers
        {
            get { return this.connectingTo; }
        }

        /// <summary>
        /// Returns the number of Leechs we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Leechs
        {
            get
            {
                int leechs = 0;
                lock (this.manager.listLock)
                    for (int i = 0; i < this.connectedPeers.Count; i++)
                        lock (this.connectedPeers[i])
                            if (!this.connectedPeers[i].IsSeeder)
                                leechs++;

                return leechs;
            }
        }

        /// <summary>
        /// Returns the number of Seeds we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Seeds
        {
            get
            {
                int seeds = 0;
                lock (this.manager.listLock)
                    for (int i = 0; i < this.connectedPeers.Count; i++)
                        lock (this.connectedPeers[i])
                            if (this.connectedPeers[i].IsSeeder)
                                seeds++;
                return seeds;
            }
        }

        #endregion


        #region Constructors

        public PeerManager(ClientEngine engine, TorrentManager manager)
        {
            this.engine = engine;
            this.manager = manager;
            this.availablePeers = new List<Peer>();
            this.banned = new List<Peer>();
            this.busy = new List<Peer>();
            this.connectedPeers = new List<Peer>();
            this.connectingTo = new List<Peer>();
        }

        #endregion Constructors


        #region Methods

        internal IEnumerable<Peer> AllPeers()
        {
            for (int i = 0; i < this.availablePeers.Count; i++)
                yield return availablePeers[i];

            for (int i = 0; i < this.connectedPeers.Count; i++)
                yield return connectedPeers[i];

            for (int i = 0; i < this.connectingTo.Count; i++)
                yield return this.connectingTo[i];

            for (int i = 0; i < this.banned.Count; i++)
                yield return this.banned[i];

            for (int i = 0; i < this.busy.Count; i++)
                yield return this.busy[i];
        }

        internal void AddPeer(Peer peer, PeerType type)
        {
            switch (type)
            {
                case (PeerType.Connected):
                    this.connectedPeers.Add(peer);
                    break;

                case (PeerType.Connecting):
                    this.connectingTo.Add(peer);
                    break;

                case (PeerType.Available):
                    this.availablePeers.Add(peer);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        internal void ClearAll()
        {
            this.availablePeers.Clear();
            this.connectedPeers.Clear();
            this.connectingTo.Clear();
        }

        internal bool Contains(Peer peer)
        {
            foreach (Peer other in AllPeers())
                if (peer.Equals(other))
                    return true;

            return false;
        }

        internal Peer Dequeue(PeerType type)
        {
            Peer id;
            switch (type)
            {
                case (PeerType.Connected):
                    id = this.connectedPeers[0];
                    this.connectedPeers.RemoveAt(0);
                    return id;

                case (PeerType.Connecting):
                    id = this.connectingTo[0];
                    this.connectingTo.RemoveAt(0);
                    return id;

                case (PeerType.Available):
                    id = this.availablePeers[0];
                    this.availablePeers.RemoveAt(0);
                    return id;

                default:
                    throw new NotSupportedException();
            }
        }

        internal void Enqueue(Peer id, PeerType type)
        {
            switch (type)
            {
                case (PeerType.Connected):
                    this.connectedPeers.Add(id);
                    return;

                case (PeerType.Connecting):
                    this.connectingTo.Add(id);
                    return;

                case (PeerType.Available):
                    this.availablePeers.Add(id);
                    return;

                default:
                    throw new NotSupportedException();
            }
        }

        internal void RemovePeer(Peer id, PeerType type)
        {
            switch (type)
            {
                case (PeerType.Connected):
                    this.connectedPeers.Remove(id);
                    break;

                case (PeerType.Connecting):
                    this.connectingTo.Remove(id);
                    break;

                case (PeerType.Available):
                    this.availablePeers.Remove(id);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        #endregion Methods
    }
}
