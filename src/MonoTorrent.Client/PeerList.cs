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

    internal class PeerList
    {
        #region Private Member Variables

        private List<PeerConnectionID> availablePeers;
        private List<PeerConnectionID> connectedPeers;
        private List<PeerConnectionID> connectingTo;
        private List<PeerConnectionID> downloadQueue;
        private List<PeerConnectionID> uploadQueue;

        #endregion Private Member Variables

        #region Internal Properties

        /// <summary>
        /// The list of peers that are available to be connected to
        /// </summary>
        internal List<PeerConnectionID> AvailablePeers
        {
            get { return this.availablePeers; }
        }

        /// <summary>
        /// The list of peers that we are currently connected to
        /// </summary>
        internal List<PeerConnectionID> ConnectedPeers
        {
            get { return this.connectedPeers; }
        }

        /// <summary>
        /// The list of peers that we are currently trying to connect to
        /// </summary>
        internal List<PeerConnectionID> ConnectingToPeers
        {
            get { return this.connectingTo; }
        }

        /// <summary>
        /// The list of peers which have data queued up to send
        /// </summary>
        internal List<PeerConnectionID> UploadQueue
        {
            get { return this.uploadQueue; }
        }

        /// <summary>
        /// The list of peers which have data queued up to download
        /// </summary>
        internal List<PeerConnectionID> DownloadQueue
        {
            get { return this.downloadQueue; }
        }

        #endregion

        #region Internal Methods

        internal void AddPeer(PeerConnectionID id, PeerType type)
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

        internal PeerConnectionID Dequeue(PeerType type)
        {
            PeerConnectionID id;
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

        internal void Enqueue(PeerConnectionID id, PeerType type)
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

        internal void RemovePeer(PeerConnectionID id, PeerType type)
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

                case(PeerType.DownloadQueue):
                    this.downloadQueue.Remove(id);
                    break;

                case(PeerType.UploadQueue):
                    this.uploadQueue.Remove(id);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        #endregion

        public PeerList()
        {
            this.availablePeers = new List<PeerConnectionID>();
            this.connectedPeers = new List<PeerConnectionID>();
            this.connectingTo = new List<PeerConnectionID>();
            this.downloadQueue = new List<PeerConnectionID>();
            this.uploadQueue = new List<PeerConnectionID>();
        }
    }
}
