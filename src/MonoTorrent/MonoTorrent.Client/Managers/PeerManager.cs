using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Tasks;

namespace MonoTorrent.Client
{
    public class PeerManager
    {
        #region Member Variables

        private ClientEngine engine;
        private TorrentManager manager;

        internal List<PeerIdInternal> ConnectedPeers = new List<PeerIdInternal>();
        internal List<PeerIdInternal> ConnectingToPeers = new List<PeerIdInternal>();

        internal MonoTorrentCollection<Peer> ActivePeers;
        internal MonoTorrentCollection<Peer> AvailablePeers;
        internal MonoTorrentCollection<Peer> BannedPeers;
        internal MonoTorrentCollection<Peer> BusyPeers;

        #endregion Member Variables


        #region Properties

        ///// <summary>
        ///// Returns the total number of peers available (including ones already connected to)
        ///// </summary>
        //public int Available
        //{
        //    get { return this.availablePeers.Count + this.activePeers.Count + this.busyPeers.Count; }
        //}

        ///// <summary>
        ///// The list of peers that are available to be connected to
        ///// </summary>
        //internal MonoTorrentCollection<Peer> AvailablePeers
        //{
        //    get { return this.availablePeers; }
        //}

        ///// <summary>
        ///// The list of peers that we are currently connected to
        ///// </summary>
        //internal MonoTorrentCollection<Peer> ActivePeers
        //{
        //    get { return this.activePeers; }
        //}

        ///// <summary>
        ///// Returns the number of Leechs we are currently connected to
        ///// </summary>
        ///// <returns></returns>
        //public int Leechs
        //{
        //    get
        //    {
        //        DelegateTask d = new DelegateTask(delegate {
        //            return Toolbox.Count<Peer>(activePeers, delegate(Peer p) { return !p.IsSeeder; });
        //        });
        //        MainLoop.QueueWait(delegate { d.Execute(); });
        //        return (int)d.Result;
        //    }
        //}

        ///// <summary>
        ///// Returns the number of Seeds we are currently connected to
        ///// </summary>
        ///// <returns></returns>
        //public int Seeds
        //{
        //    get
        //    {
        //        DelegateTask d = new DelegateTask(delegate {
        //            return Toolbox.Count<Peer>(activePeers, delegate(Peer p) { return p.IsSeeder; });
        //        });
        //        MainLoop.QueueWait(delegate { d.Execute(); });
        //        return (int)d.Result;
        //    }
        //}

        #endregion


        #region Constructors

        public PeerManager(ClientEngine engine, TorrentManager manager)
        {
            this.engine = engine;
            this.manager = manager;
            this.ActivePeers = new MonoTorrentCollection<Peer>();
            this.AvailablePeers = new MonoTorrentCollection<Peer>();
            this.BannedPeers = new MonoTorrentCollection<Peer>();
            this.BusyPeers = new MonoTorrentCollection<Peer>();
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

        #endregion Methods
    }
}
