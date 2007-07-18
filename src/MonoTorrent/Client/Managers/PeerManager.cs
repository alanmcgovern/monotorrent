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
        private List<PeerIdInternal> connectedPeers;
        private List<PeerIdInternal> connectingTo;
        private List<PeerIdInternal> downloadQueue;
        private List<PeerIdInternal> uploadQueue;
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
        internal List<PeerIdInternal> ConnectedPeers
        {
            get { return this.connectedPeers; }
        }

        /// <summary>
        /// The list of peers that we are currently trying to connect to
        /// </summary>
        internal List<PeerIdInternal> ConnectingToPeers
        {
            get { return this.connectingTo; }
        }

        /// <summary>
        /// The list of peers which have data queued up to download
        /// </summary>
        internal List<PeerIdInternal> DownloadQueue
        {
            get { return this.downloadQueue; }
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
                            if (!this.connectedPeers[i].Peer.IsSeeder)
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
                            if (this.connectedPeers[i].Peer.IsSeeder)
                                seeds++;
                return seeds;
            }
        }

        /// <summary>
        /// The list of peers which have data queued up to send
        /// </summary>
        internal List<PeerIdInternal> UploadQueue
        {
            get { return this.uploadQueue; }
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
            this.connectedPeers = new List<PeerIdInternal>();
            this.connectingTo = new List<PeerIdInternal>();
            this.downloadQueue = new List<PeerIdInternal>();
            this.uploadQueue = new List<PeerIdInternal>();
        }

        #endregion Constructors


        #region Methods

        internal IEnumerable<Peer> AllPeers()
        {
            for (int i = 0; i < this.availablePeers.Count; i++)
                yield return availablePeers[i];

            for (int i = 0; i < this.connectedPeers.Count; i++)
                yield return connectedPeers[i].Peer;

            for (int i = 0; i < this.connectingTo.Count; i++)
                yield return this.connectingTo[i].Peer;

            for (int i = 0; i < this.banned.Count; i++)
                yield return this.banned[i];

            for (int i = 0; i < this.busy.Count; i++)
                yield return this.busy[i];
        }

        internal void AddPeer(PeerIdInternal id, PeerType type)
        {
            switch (type)
            {
                case (PeerType.Connected):
                    this.connectedPeers.Add(id);
                    break;

                case (PeerType.Connecting):
                    this.connectingTo.Add(id);
                    break;

                case (PeerType.Available):
                    this.availablePeers.Add(id.Peer);
                    break;

                case (PeerType.DownloadQueue):
                    this.downloadQueue.Add(id);
                    break;

                case (PeerType.UploadQueue):
                    this.uploadQueue.Remove(id);
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
            this.downloadQueue.Clear();
            this.uploadQueue.Clear();
        }

        internal bool Contains(Peer peer)
        {
            foreach (Peer other in AllPeers())
                if (peer.Equals(other))
                    return true;

            return false;
        }

        internal PeerIdInternal Dequeue(PeerType type)
        {
            PeerIdInternal id;
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
                    id = new PeerIdInternal(this.availablePeers[0], manager);
                    this.availablePeers.RemoveAt(0);
                    return id;

                case (PeerType.DownloadQueue):
                    id = this.downloadQueue[0];
                    this.downloadQueue.RemoveAt(0);
                    return id;

                case (PeerType.UploadQueue):
                    id = this.uploadQueue[0];
                    this.uploadQueue.RemoveAt(0);
                    return id;

                default:
                    throw new NotSupportedException();
            }
        }

        internal void Enqueue(PeerIdInternal id, PeerType type)
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
                    this.availablePeers.Add(id.Peer);
                    return;

                case (PeerType.DownloadQueue):
                    this.downloadQueue.Add(id);
                    return;

                case (PeerType.UploadQueue):
                    this.uploadQueue.Add(id);
                    return;

                default:
                    throw new NotSupportedException();
            }
        }

        internal void RemovePeer(PeerIdInternal id, PeerType type)
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
                    this.availablePeers.Remove(id.Peer);
                    break;

                case (PeerType.DownloadQueue):
                    this.downloadQueue.Remove(id);
                    break;

                case (PeerType.UploadQueue):
                    this.uploadQueue.Remove(id);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        #endregion Methods
    }
}
