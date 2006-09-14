//
// ClientEngine.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//



using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using MonoTorrent.Common;
using System.Timers;
using System.Net;
using MonoTorrent.Client.PeerMessages;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
{
    /// <summary>
    /// The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IClientEngine, IDisposable
    {
        #region GlobalSupports
        public const bool SupportsFastPeer = false;
        #endregion

        #region Member Variables
        private System.Timers.Timer timer;


        /// <summary>
        /// Returns the engines PeerID
        /// </summary>
        public static string PeerId
        {
            get { return peerId; }
        }
        private static string peerId;


        /// <summary>
        /// The engines settings
        /// </summary>
        public IEngineSettings Settings
        {
            get { return this.settings; }
            set { this.settings = value; }
        }
        private IEngineSettings settings;

        /// <summary>
        /// The default settings to be used by newly added torrents
        /// </summary>
        public TorrentSettings DefaultTorrentSettings
        {
            get { return this.defaultTorrentSettings; }
            set { this.defaultTorrentSettings = value; }
        }
        private TorrentSettings defaultTorrentSettings;


        /// <summary>
        /// The ITorrentManager's loaded into the engine
        /// </summary>
        public Dictionary<string, ITorrentManager> Torrents
        {
            get { return this.torrents; }
            set { this.torrents = value; }
        }
        private Dictionary<string, ITorrentManager> torrents;


        /// <summary>
        /// The connection manager which manages all the connections for the library
        /// </summary>
        public static ConnectionManager connectionManager;
        private ConnectionListener listener;

        private AsyncCallback peerHandshakeRecieved;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new ClientEngine
        /// </summary>
        /// <param name="engineSettings">The engine settings to use</param>
        /// <param name="defaultTorrentSettings">The default settings for new torrents</param>
        public ClientEngine(EngineSettings engineSettings, TorrentSettings defaultTorrentSettings)
        {
            peerId = GeneratePeerId();

            connectionManager = new ConnectionManager(engineSettings);
            this.settings = engineSettings;
            this.defaultTorrentSettings = defaultTorrentSettings;
            this.listener = new ConnectionListener(engineSettings.ListenPort, new AsyncCallback(IncomingConnectionRecieved));
#warning I don't like this timer, but is there any other better way to do it?
            this.timer = new System.Timers.Timer(50);
            this.timer.Elapsed += new ElapsedEventHandler(LogicTick);
            this.torrents = new Dictionary<string, ITorrentManager>();
            this.peerHandshakeRecieved = new AsyncCallback(this.onPeerHandshakeRecieved);
        }
        #endregion

        #region Start/Stop/Pause
        /// <summary>
        /// Starts all torrents in the engine if they are not already started
        /// </summary>
        public void Start()
        {
            foreach (KeyValuePair<string, ITorrentManager> keypair in this.torrents)
                ((TorrentManager)keypair.Value).Start();

            timer.Start();           // Start the timer
            if (!this.listener.IsListening)
                this.listener.Start();      // Start Listening for connections
        }


        /// <summary>
        /// Starts the specified torrent
        /// </summary>
        /// <param name="manager">The torrent to start</param>
        public void Start(ITorrentManager manager)
        {
            if (manager is TorrentManager)
                ((TorrentManager)manager).Start();

            timer.Start();           // Start logic ticking
            if (!this.listener.IsListening)
                this.listener.Start();      // Start Listening for connections
        }


        /// <summary>
        /// Stops all torrents in the engine and flushes out all peer information
        /// </summary>
        public WaitHandle[] Stop()
        {
            int i = 0;
            WaitHandle[] waitHandles = new WaitHandle[this.torrents.Count];
            foreach (KeyValuePair<string, ITorrentManager> keypair in this.torrents)
                waitHandles[i++] = ((TorrentManager)keypair.Value).Stop();

            timer.Enabled = false;
            if (this.listener.IsListening)
                this.listener.Stop();

            return waitHandles;
        }


        /// <summary>
        /// Stops the specified torrent and flushes out all peer information
        /// </summary>
        /// <param name="manager"></param>
        public void Stop(ITorrentManager manager)
        {
            if (manager is TorrentManager)
                ((TorrentManager)manager).Stop();

            foreach (KeyValuePair<string, ITorrentManager> keypair in this.torrents)
                if (keypair.Value.State != TorrentState.Paused && keypair.Value.State != TorrentState.Stopped)
                    return;

            timer.Enabled = false;      // All the torrents are stopped, so stop ticking
            if (this.listener.IsListening)
                this.listener.Stop();
        }


        /// <summary>
        /// Stops all torrents in the engine but retains all peer information
        /// </summary>
        public void Pause()
        {
            foreach (KeyValuePair<string, ITorrentManager> keypair in this.torrents)
                ((TorrentManager)keypair.Value).Pause();

            timer.Enabled = false;
            if (this.listener.IsListening)
                this.listener.Stop();
        }


        /// <summary>
        /// Stops the specified torrent but retains all peer information
        /// </summary>
        /// <param name="manager"></param>
        public void Pause(ITorrentManager manager)
        {
            if (manager is TorrentManager)
                ((TorrentManager)manager).Pause();

            foreach (KeyValuePair<string, ITorrentManager> keypair in this.torrents)
                if (keypair.Value.State != TorrentState.Paused && keypair.Value.State != TorrentState.Stopped)
                    return;

            timer.Enabled = false;      // All the torrents are stopped, so stop ticking
            if (this.listener.IsListening)
                this.listener.Stop();
        }
        #endregion

        #region Load/Remove Torrents
        /// <summary>
        /// Loads a .torrent file from the specified path.
        /// </summary>
        /// <param name="path">The path to the .torrent file</param>
        /// <returns>A TorrentManager used to control the torrent</returns>
        public ITorrentManager LoadTorrent(string path)
        {
            return this.LoadTorrent(path, this.settings.DefaultSavePath, this.defaultTorrentSettings);
        }


        /// <summary>
        /// Loads a .torrent file from the specified URL
        /// </summary>
        /// <param name="url">The URL to download the .torrent from</param>
        /// <param name="location">The path on your computer to download the .torrent to before it's loaded into the engine</param>
        /// <returns></returns>
        public ITorrentManager LoadTorrent(Uri url, string location)
        {
            return this.LoadTorrent(url, location, settings.DefaultSavePath, this.defaultTorrentSettings);
        }
        

        /// <summary>
        /// Loads a .torrent file from the specified URL
        /// </summary>
        /// <param name="url">The URL to download the .torrent from</param>
        /// <param name="location">The path on your computer to download the .torrent to before it's loaded into the engine</param>
        /// <param name="savePath">The path to download the files to</param>
        /// <returns></returns>
        public ITorrentManager LoadTorrent(Uri url, string location, string savePath)
        {
            return this.LoadTorrent(url, location, savePath, this.defaultTorrentSettings);
        }


        /// <summary>
        /// Loads a .torrent from the specified URL
        /// </summary>
        /// <param name="url">The URL to download the .torrent from</param>
        /// <param name="location">The path on your computer to download the .torrent to before it's loaded into the engine</param>
        /// <param name="savePath">The path to download the files to</param>
        /// <param name="settings">The TorrentSettings to initialise the torrent with</param>
        /// <returns></returns>
        public ITorrentManager LoadTorrent(Uri url, string location, string savePath, TorrentSettings settings)
        {
            WebClient client = new WebClient();
            client.DownloadFile(url, location);
            return this.LoadTorrent(location, savePath, settings);
        }


        /// <summary>
        /// Loads a .torrent file from the specified path
        /// </summary>
        /// <param name="path">The path of the .torrent file</param>
        /// <param name="savePath">The path to download the files to</param>
        /// <returns>A TorrentManager used to control the torrent</returns>
        public ITorrentManager LoadTorrent(string path, string savePath)
        {
            return this.LoadTorrent(path, savePath, this.defaultTorrentSettings);
        }

        
        /// <summary>
        /// Loads a .torrent file from the specified path
        /// </summary>
        /// <param name="path">The path to the .torrent file</param>
        /// <param name="savePath">The path to download the files to</param>
        /// <param name="torrentSettings">The TorrentSettings to initialise the torrent with</param>
        /// <returns>A TorrentManager used to control the torrent</returns>
        public ITorrentManager LoadTorrent(string path, string savePath, TorrentSettings settings)
        {
            Torrent torrent = new Torrent();
            torrent.LoadTorrent(path);

            if (this.torrents.ContainsKey(BitConverter.ToString(torrent.InfoHash)))
                throw new TorrentException("The torrent is already in the engine");

            TorrentManager manager = new TorrentManager(torrent, savePath, settings);
            this.torrents.Add(BitConverter.ToString(torrent.InfoHash), manager);

            if (File.Exists(torrent.TorrentPath + ".fresume"))
                if (LoadFastResume(manager))
                    manager.HashChecked = true;

            return (manager);
        }

        private bool LoadFastResume(TorrentManager manager)
        {
            try
            {
                XmlSerializer fastResume = new XmlSerializer(typeof(int[]));
                using (FileStream file = File.OpenRead(manager.Torrent.TorrentPath + ".fresume"))
                    manager.PieceManager.MyBitField.FromArray((int[])fastResume.Deserialize(file));

                return true;
            }
#warning Don't catch everything...
            catch
            {
                return false;
            }
        }


        public void RemoveTorrent(ITorrentManager manager)
        {
            ((TorrentManager)manager).Stop();
            ((TorrentManager)manager).Dispose();
            this.torrents.Remove(BitConverter.ToString(manager.Torrent.InfoHash));
        }


        public void RemoveAll()
        {
            foreach (TorrentManager manager in this.torrents.Values)
            {
                manager.Stop();
                manager.Dispose();
                this.torrents.Remove(BitConverter.ToString(manager.Torrent.InfoHash));
            }

#warning Use waithandles to wait for the trackerupdate to send
            System.Threading.Thread.Sleep(5000);
        }
        #endregion

        #region Misc
        private string GeneratePeerId()
        {
            StringBuilder sb = new StringBuilder(20);
            Random rand = new Random((int)DateTime.Now.TimeOfDay.TotalMilliseconds);

            sb.Append(Common.VersionInfo.ClientVersion);
            for (int i = 0; i < 12; i++)
                sb.Append(rand.Next(0, 9));

            return sb.ToString();
        }

        private void LogicTick(object sender, ElapsedEventArgs e)
        {
            foreach (KeyValuePair<string, ITorrentManager> keypair in this.torrents)
            {
                switch (keypair.Value.State)
                {
                    case (TorrentState.Downloading):
                        ((TorrentManager)keypair.Value).DownloadLogic();
                        break;

                    case (TorrentState.Seeding):
                        ((TorrentManager)keypair.Value).SeedingLogic();
                        break;

                    case (TorrentState.SuperSeeding):
                        ((TorrentManager)keypair.Value).SuperSeedingLogic();
                        break;

                    default:
                        break;  // Do nothing.
                }
            }
        }

        private void IncomingConnectionRecieved(IAsyncResult result)
        {
            PeerConnectionID id = null;
            try
            {
                Socket peerSocket = ((Socket)result.AsyncState);
                if (!peerSocket.Connected)
                    return;

                peerSocket = peerSocket.EndAccept(result);

                Peer peer = new Peer(string.Empty, peerSocket.RemoteEndPoint.ToString());
                peer.Connection = new TCPConnection(peerSocket, 0, new NoEncryption());
                id = new PeerConnectionID(peer);

                id.Peer.Connection.BytesRecieved = 0;
                id.Peer.Connection.BytesToRecieve = 68;

                id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, peerHandshakeRecieved, id);
            }
            catch (SocketException ex)
            {
                if (id != null)
                    this.CleanupSocket(id);
            }
        }

        private void onPeerHandshakeRecieved(IAsyncResult result)
        {
            TorrentManager man = null;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;

            try
            {
                int bytesRecieved = id.Peer.Connection.EndReceive(result);
                if (bytesRecieved == 0)
                {
                    CleanupSocket(id);
                    return;
                }

                id.Peer.Connection.BytesRecieved += bytesRecieved;
                if (id.Peer.Connection.BytesRecieved != id.Peer.Connection.BytesToRecieve)
                {
                    id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesRecieved, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesRecieved, SocketFlags.None, peerHandshakeRecieved, id);
                    return;
                }


                HandshakeMessage handshake = new HandshakeMessage();
                handshake.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);

                foreach (TorrentManager manager in this.torrents.Values)
                    if (ToolBox.ByteMatch(handshake.infoHash, manager.Torrent.InfoHash))
                        man = manager;

                if (man == null)        // We're not hosting that torrent
                {
                    CleanupSocket(id);
                    return;
                }

                id.Peer.PeerId = handshake.PeerId;
                id.TorrentManager = man;
                handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, ClientEngine.peerId, VersionInfo.ProtocolStringV100);

                BitfieldMessage bf = new BitfieldMessage(id.TorrentManager.PieceManager.MyBitField);

                id.Peer.Connection.BytesSent = 0;
                id.Peer.Connection.BytesToSend = handshake.Encode(id.Peer.Connection.sendBuffer, 0);
                id.Peer.Connection.BytesToSend += bf.Encode(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesToSend);

                id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend, SocketFlags.None, new AsyncCallback(ClientEngine.connectionManager.IncomingConnectionAccepted), id);
                id.Peer.Connection.ProcessingQueue = false;
                return;
            }

            catch (SocketException ex)
            {
                CleanupSocket(id);
            }
        }

        private void CleanupSocket(PeerConnectionID id)
        {
            id.Peer.Connection.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        public void Dispose(bool disposing)
        {
            this.listener.Dispose();
            this.timer.Dispose();
        }
        #endregion
    }
}
