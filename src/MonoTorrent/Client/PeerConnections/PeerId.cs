using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
{
    public class PeerId
    {
        #region Member Variables

        private bool amChoking;
        private bool amInterested;
        private int amRequestingPiecesCount;
        private BitField bitField;
        private Software clientApp;
        private IEncryptorInternal encryptor;
        private int hashFails;
        private bool isChoking;
        private bool isInterested;
        private int isRequestingPiecesCount;
        private bool isSeeder;
        private bool isValid;
        private string location;
        private ConnectionMonitor monitor;
        private string peerId;
        private int piecesSent;
        private int sendQueueLength;
        private bool supportsFastPeer;
        private TorrentManager manager;

        #endregion Member Variables


        #region Properties

        public bool AmChoking
        {
            get { return this.amChoking; }
        }

        public bool AmInterested
        {
            get { return this.amInterested; }
        }

        public int AmRequestingPiecesCount
        {
            get { return this.amRequestingPiecesCount; }
        }

        public BitField Bitfield
        {
            get { return this.bitField; }
        }

        public Software ClientSoftware
        {
            get { return this.clientApp; }
        }

        public IEncryptor Encryptor
        {
            get { return this.encryptor; }
        }

        public int HashFails
        {
            get { return this.hashFails; }
        }

        public bool IsChoking
        {
            get { return this.isChoking; }
        }

        public bool IsInterested
        {
            get { return this.isInterested; }
        }

        public int IsRequestingPiecesCount
        {
            get { return this.isRequestingPiecesCount; }
        }

        public bool IsSeeder
        {
            get { return this.isSeeder; }
        }

        public bool IsValid
        {
            get { return isValid; }
            internal set { isValid = value; }
        }

        public string Location
        {
            get { return this.location; }
        }

        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }

        public string Name
        {
            get { return this.peerId; }
        }

        public int PiecesSent
        {
            get { return this.piecesSent; }
        }

        public int SendQueueLength
        {
            get { return this.sendQueueLength; }
        }

        public bool SupportsFastPeer
        {
            get { return this.supportsFastPeer; }
        }

        public TorrentManager TorrentManager
        {
            get { return this.manager; }
        }

        #endregion Properties


        #region Constructors

        internal PeerId()
        {
            this.isValid = true;
        }

        #endregion


        #region Methods

        internal void UpdateStats(PeerIdInternal id)
        {
            amChoking = id.Connection.AmChoking;
            amInterested = id.Connection.AmInterested;
            amRequestingPiecesCount = id.Connection.AmRequestingPiecesCount;
            bitField = id.Connection.BitField;
            clientApp = id.Connection.ClientApp;
            encryptor = id.Connection.Encryptor;
            hashFails = id.Peer.TotalHashFails;
            isChoking = id.Connection.IsChoking;
            isInterested = id.Connection.IsInterested;
            isRequestingPiecesCount = id.Connection.IsRequestingPiecesCount;
            isSeeder = id.Peer.IsSeeder;
            location = id.Peer.Location;
            monitor = id.Connection.Monitor;
            peerId = id.Peer.PeerId;
            piecesSent = id.Connection.PiecesSent;
            sendQueueLength = id.Connection.QueueLength;
            supportsFastPeer = id.Connection.SupportsFastPeer;
            manager = id.TorrentManager;
        }

        #endregion
    }
}
