using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class PeerManager
    {
        #region Member Variables

        internal List<PeerId> ConnectedPeers = new List<PeerId>();
        internal List<Peer> ConnectingToPeers = new List<Peer>();

        internal List<Peer> ActivePeers;
        internal List<Peer> AvailablePeers;
        internal List<Peer> BannedPeers;
        internal List<Peer> BusyPeers;

        #endregion Member Variables


        #region Properties

        public int Available { get; private set; }

        /// <summary>
        /// Returns the number of Leechs we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Leechs { get; private set; }

        /// <summary>
        /// Returns the number of Seeds we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Seeds { get; private set; }

        #endregion


        #region Constructors

        public PeerManager()
        {
            this.ActivePeers = new List<Peer>();
            this.AvailablePeers = new List<Peer>();
            this.BannedPeers = new List<Peer>();
            this.BusyPeers = new List<Peer>();
        }

        #endregion Constructors


        #region Methods

        internal IEnumerable<Peer> AllPeers()
        {
            for (int i = 0; i < AvailablePeers.Count; i++)
                yield return AvailablePeers[i];

            for (int i = 0; i < ActivePeers.Count; i++)
                yield return ActivePeers[i];

            for (int i = 0; i < BannedPeers.Count; i++)
                yield return BannedPeers[i];

            for (int i = 0; i < BusyPeers.Count; i++)
                yield return BusyPeers[i];
        }

        internal void ClearAll()
        {
            this.ActivePeers.Clear();
            this.AvailablePeers.Clear();
            this.BannedPeers.Clear();
            this.BusyPeers.Clear();
        }

        internal bool Contains(Peer peer)
        {
            foreach (Peer other in AllPeers())
                if (peer.Equals(other))
                    return true;

            return false;
        }

        internal void UpdatePeerCounts ()
        {
            int seeds = 0;
            int leeches = 0;
            for (int i = 0; i < ActivePeers.Count; i++) {
                if (ActivePeers[i].IsSeeder)
                    seeds++;
                else
                    leeches++;
            }

            Available = AvailablePeers.Count;
            Seeds = seeds;
            Leechs = leeches;
        }
        #endregion Methods
    }
}
