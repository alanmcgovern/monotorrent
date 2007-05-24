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
        DownloadQueue
    }

    public class PeerList
    {
        #region Private Member Variables

        private TorrentManager manager;
        private PeerIdCollection availablePeers;
        private PeerIdCollection connectedPeers;
        private PeerIdCollection connectingTo;
        private PeerIdCollection downloadQueue;
        private PeerIdCollection uploadQueue;

        #endregion Private Member Variables


        #region Internal Properties

        /// <summary>
        /// The list of peers that are available to be connected to
        /// </summary>
        internal PeerIdCollection AvailablePeers
        {
            get { return this.availablePeers; }
        }

        /// <summary>
        /// The list of peers that we are currently connected to
        /// </summary>
        internal PeerIdCollection ConnectedPeers
        {
            get { return this.connectedPeers; }
        }

        /// <summary>
        /// The list of peers that we are currently trying to connect to
        /// </summary>
        internal PeerIdCollection ConnectingToPeers
        {
            get { return this.connectingTo; }
        }

        /// <summary>
        /// The list of peers which have data queued up to send
        /// </summary>
        internal PeerIdCollection UploadQueue
        {
            get { return this.uploadQueue; }
        }

        /// <summary>
        /// The list of peers which have data queued up to download
        /// </summary>
        internal PeerIdCollection DownloadQueue
        {
            get { return this.downloadQueue; }
        }

        #endregion


        #region Constructors

        internal PeerList(TorrentManager manager)
        {
            this.manager = manager;
            this.availablePeers = new PeerIdCollection();
            this.connectedPeers = new PeerIdCollection();
            this.connectingTo = new PeerIdCollection();
            this.downloadQueue = new PeerIdCollection();
            this.uploadQueue = new PeerIdCollection();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Returns the total number of peers available (including ones already connected to)
        /// </summary>
        public int Available
        {
            get { return this.availablePeers.Count + this.connectedPeers.Count + this.connectingTo.Count; }
        }


        /// <summary>
        /// Returns the number of Leechs we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Leechs()
        {
            int leechs = 0;
            lock (this.manager.listLock)
                for (int i = 0; i < this.connectedPeers.Count; i++)
                    lock (this.connectedPeers[i])
                        if (!this.connectedPeers[i].Peer.IsSeeder)
                            leechs++;

            return leechs;
        }


        /// <summary>
        /// Returns the number of Seeds we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Seeds()
        {
            int seeds = 0;
            lock (this.manager.listLock)
                for (int i = 0; i < this.connectedPeers.Count; i++)
                    lock (this.connectedPeers[i])
                        if (this.connectedPeers[i].Peer.IsSeeder)
                            seeds++;
            return seeds;
        }

        #endregion


        #region Internal Methods

        internal void AddPeer(PeerId id, PeerType type)
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
                    this.availablePeers.Add(id);
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

        internal PeerId Dequeue(PeerType type)
        {
            PeerId id;
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

        internal void Enqueue(PeerId id, PeerType type)
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

        internal void RemovePeer(PeerId id, PeerType type)
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

        #endregion
    }
}
