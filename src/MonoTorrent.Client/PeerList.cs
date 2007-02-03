using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class PeerList
    {
        /// <summary>
        /// The list of peers that are available to be connected to
        /// </summary>
        internal Peers AvailablePeers
        {
            get { return this.availablePeers; }
            set { this.availablePeers = value; }
        }
        private Peers availablePeers;


        /// <summary>
        /// The list of peers that we are currently connected to
        /// </summary>
        internal Peers ConnectedPeers
        {
            get { return this.connectedPeers; }
            set { this.connectedPeers = value; }
        }
        private Peers connectedPeers;


        /// <summary>
        /// The list of peers that we are currently trying to connect to
        /// </summary>
        internal Peers ConnectingToPeers
        {
            get { return this.connectingTo; }
            set { this.connectingTo = value; }
        }
        private Peers connectingTo;

        internal enum PeerType {Connecting, Connected, Available}


        internal void AddPeer(PeerConnectionID id, PeerType type)
        {
            if (this.availablePeers.Contains(id) || this.connectingTo.Contains(id) || this.connectedPeers.Contains(id))
            {
                string s = "";
            }
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
            }

            if (this.availablePeers.Contains(id) || this.connectingTo.Contains(id) || this.connectedPeers.Contains(id))
            {
                string s = "";
            }
        }


        public PeerList()
        {
        }
    }
}
