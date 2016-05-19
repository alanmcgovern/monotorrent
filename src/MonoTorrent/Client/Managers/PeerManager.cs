using System.Collections.Generic;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class PeerManager
    {
        #region Constructors

        public PeerManager()
        {
            ActivePeers = new List<Peer>();
            AvailablePeers = new List<Peer>();
            BannedPeers = new List<Peer>();
            BusyPeers = new List<Peer>();
        }

        #endregion Constructors

        #region Member Variables

        internal List<PeerId> ConnectedPeers = new List<PeerId>();
        internal List<Peer> ConnectingToPeers = new List<Peer>();

        internal List<Peer> ActivePeers;
        internal List<Peer> AvailablePeers;
        internal List<Peer> BannedPeers;
        internal List<Peer> BusyPeers;

        #endregion Member Variables

        #region Properties

        public int Available
        {
            get { return AvailablePeers.Count; }
        }

        /// <summary>
        ///     Returns the number of Leechs we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Leechs
        {
            get
            {
                return
                    (int)
                        ClientEngine.MainLoop.QueueWait(
                            delegate { return Toolbox.Count(ActivePeers, delegate(Peer p) { return !p.IsSeeder; }); });
            }
        }

        /// <summary>
        ///     Returns the number of Seeds we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Seeds
        {
            get
            {
                return
                    (int)
                        ClientEngine.MainLoop.QueueWait(
                            delegate { return Toolbox.Count(ActivePeers, delegate(Peer p) { return p.IsSeeder; }); });
            }
        }

        #endregion

        #region Methods

        internal IEnumerable<Peer> AllPeers()
        {
            for (var i = 0; i < AvailablePeers.Count; i++)
                yield return AvailablePeers[i];

            for (var i = 0; i < ActivePeers.Count; i++)
                yield return ActivePeers[i];

            for (var i = 0; i < BannedPeers.Count; i++)
                yield return BannedPeers[i];

            for (var i = 0; i < BusyPeers.Count; i++)
                yield return BusyPeers[i];
        }

        internal void ClearAll()
        {
            ActivePeers.Clear();
            AvailablePeers.Clear();
            BannedPeers.Clear();
            BusyPeers.Clear();
        }

        internal bool Contains(Peer peer)
        {
            foreach (var other in AllPeers())
                if (peer.Equals(other))
                    return true;

            return false;
        }

        #endregion Methods
    }
}