using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    internal enum PeerType
    {
        Active,
        Available,
        Banned,
        Busy
    }

    public class PeerManager
    {
        #region Member Variables

        private ClientEngine engine;
        private TorrentManager manager;
        private MonoTorrentCollection<Peer> activePeers;
        private MonoTorrentCollection<Peer> availablePeers;
        private MonoTorrentCollection<Peer> bannedPeers;
        private MonoTorrentCollection<Peer> busyPeers;

        #endregion Member Variables


        #region Properties

        /// <summary>
        /// Returns the total number of peers available (including ones already connected to)
        /// </summary>
        public int Available
        {
            get { return this.availablePeers.Count + this.activePeers.Count + this.busyPeers.Count; }
        }

        /// <summary>
        /// The list of peers that are available to be connected to
        /// </summary>
        internal MonoTorrentCollection<Peer> AvailablePeers
        {
            get { return this.availablePeers; }
        }

        /// <summary>
        /// The list of peers that we are currently connected to
        /// </summary>
        internal MonoTorrentCollection<Peer> ActivePeers
        {
            get { return this.activePeers; }
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
                    for (int i = 0; i < this.activePeers.Count; i++)
                        lock (this.activePeers[i])
                            if (!this.activePeers[i].IsSeeder)
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
                    for (int i = 0; i < this.activePeers.Count; i++)
                        lock (this.activePeers[i])
                            if (this.activePeers[i].IsSeeder)
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
            this.activePeers = new MonoTorrentCollection<Peer>();
            this.availablePeers = new MonoTorrentCollection<Peer>();
            this.bannedPeers = new MonoTorrentCollection<Peer>();
            this.busyPeers = new MonoTorrentCollection<Peer>();
        }

        #endregion Constructors


        #region Methods

        internal IEnumerable<Peer> AllPeers()
        {
            for (int i = 0; i < availablePeers.Count; i++)
                yield return availablePeers[i];

            for (int i = 0; i < activePeers.Count; i++)
                yield return activePeers[i];

            for (int i = 0; i < bannedPeers.Count; i++)
                yield return bannedPeers[i];

            for (int i = 0; i < busyPeers.Count; i++)
                yield return busyPeers[i];
        }

        internal void AddPeer(Peer peer, PeerType type)
        {
            switch (type)
            {
                case (PeerType.Active):
                    this.activePeers.Add(peer);
                    break;

                case (PeerType.Available):
                    this.availablePeers.Add(peer);
                    break;

                case(PeerType.Banned):
                    bannedPeers.Add(peer);
                    break;

                case(PeerType.Busy):
                    busyPeers.Add(peer);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        internal void ClearAll()
        {
            this.activePeers.Clear();
            this.availablePeers.Clear();
            this.bannedPeers.Clear();
            this.busyPeers.Clear();
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
            switch (type)
            {
                case (PeerType.Active):
                    return activePeers.Dequeue();

                case (PeerType.Available):
                    return availablePeers.Dequeue();

                case (PeerType.Banned):
                    return bannedPeers.Dequeue();

                case (PeerType.Busy):
                    return busyPeers.Dequeue();

                default:
                    throw new NotSupportedException();
            }
        }

        internal void Enqueue(Peer id, PeerType type)
        {
            switch (type)
            {
                case (PeerType.Active):
                    activePeers.Add(id);
                    break;

                case (PeerType.Available):
                     availablePeers.Add(id);
                     break;

                case (PeerType.Banned):
                    bannedPeers.Add(id);
                    break;

                case (PeerType.Busy):
                    busyPeers.Add(id);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        internal void RemovePeer(Peer id, PeerType type)
        {
            switch (type)
            {
                case (PeerType.Active):
                    activePeers.Remove(id);
                    break;

                case (PeerType.Available):
                    availablePeers.Remove(id);
                    break;

                case (PeerType.Banned):
                    bannedPeers.Remove(id);
                    break;

                case (PeerType.Busy):
                    busyPeers.Remove(id);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        #endregion Methods
    }
}
