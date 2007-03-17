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
using System.Timers;
using System.Net;
using MonoTorrent.Client.PeerMessages;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;
using Nat;

namespace MonoTorrent.Client
{
    /// <summary>
    /// The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IDisposable
    {
        #region Global Supports
        internal static readonly bool SupportsFastPeer = true;
        internal static readonly bool SupportsEncryption = true;
        #endregion


        #region Private Member Variables
        /// <summary>
        /// A logic tick will be performed every TickLength miliseconds
        /// </summary>
        internal const int TickLength = 25;

        private PortMapper portMapper;
        #endregion


        #region Member Variables
        /// <summary>
        /// This manager is used to control Send/Receive buffer allocations for all running torrents
        /// </summary>
        internal static readonly BufferManager BufferManager = new BufferManager();


        /// <summary>
        /// The connection manager which manages all the connections for the library
        /// </summary>
        public static ConnectionManager ConnectionManager;


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
        /// True if the engine has been started
        /// </summary>
        public bool IsRunning
        {
            get { return this.timer.Enabled; }
        }


        /// <summary>
        /// Listens for incoming connections and passes them off to the correct TorrentManager
        /// </summary>
        private ConnectionListener listener;


        /// <summary>
        /// The callback to invoke when we receive a peer handshake.
        /// </summary>
        private AsyncCallback peerHandshakeReceived;

        private EncryptorReadyHandler onEncryptorReadyHandler;
        private EncryptorIOErrorHandler onEncryptorIOErrorHandler;
        private EncryptorEncryptionErrorHandler onEncryptorEncryptionErrorHandler;

        /// <summary>
        /// Returns the engines PeerID
        /// </summary>
        public static string PeerId
        {
            get { return peerId; }
        }
        private static readonly string peerId = GeneratePeerId();


        /// <summary>
        /// The engines settings
        /// </summary>
        public EngineSettings Settings
        {
            get { return this.settings; }
            set { this.settings = value; }
        }
        private EngineSettings settings;


        /// <summary>
        /// The timer used to call the logic methods for the torrent managers
        /// </summary>
        private System.Timers.Timer timer;
        private int tickCount;


        /// <summary>
        /// The TorrentManager's loaded into the engine
        /// </summary>
        public List<TorrentManager> Torrents
        {
            get { return this.torrents; }
            set { this.torrents = value; }
        }
        private List<TorrentManager> torrents;
        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new ClientEngine
        /// </summary>
        /// <param name="engineSettings">The engine settings to use</param>
        /// <param name="defaultTorrentSettings">The default settings for new torrents</param>
        public ClientEngine(EngineSettings engineSettings, TorrentSettings defaultTorrentSettings)
        {
            ClientEngine.ConnectionManager = new ConnectionManager(engineSettings);
            this.settings = engineSettings;
            this.defaultTorrentSettings = defaultTorrentSettings;
            this.listener = new ConnectionListener(engineSettings.ListenPort, new AsyncCallback(this.IncomingConnectionReceived));
#warning I don't like this timer, but is there any other better way to do it?
            this.timer = new System.Timers.Timer(TickLength);
            this.timer.Elapsed += new ElapsedEventHandler(LogicTick);
            this.torrents = new List<TorrentManager>();
            this.peerHandshakeReceived = new AsyncCallback(this.onPeerHandshakeReceived);

            this.onEncryptorReadyHandler = new EncryptorReadyHandler(onEncryptorReady);
            this.onEncryptorIOErrorHandler = new EncryptorIOErrorHandler(onEncryptorError);
            this.onEncryptorEncryptionErrorHandler = new EncryptorEncryptionErrorHandler(onEncryptorError);

            // If uPnP support has been enabled
            if (this.settings.UsePnP)
            {
                this.portMapper = new PortMapper();
                this.portMapper.RouterFound += new EventHandler(portMapper_RouterFound);
                this.portMapper.Start();
            }
        }

        void portMapper_RouterFound(object sender, EventArgs e)
        {
            this.portMapper.MapPort(this.settings.ListenPort);
        }

        #endregion


        #region Start/Stop/Pause
        /// <summary>
        /// Starts all torrents in the engine if they are not already started
        /// </summary>
        public void Start()
        {
            for (int i = 0; i < this.torrents.Count; i++)
                Start(this.torrents[i]);
        }


        /// <summary>
        /// Starts the specified torrent
        /// </summary>
        /// <param name="manager">The torrent to start</param>
        public void Start(TorrentManager manager)
        {
            if (!this.listener.IsListening)
                this.listener.Start();      // Start Listening for connections

            if (!timer.Enabled)
                timer.Enabled = true;       // Start logic ticking

            if (manager.State == TorrentState.Stopped || manager.State == TorrentState.Paused)
                manager.Start();
        }


        /// <summary>
        /// Stops all torrents in the engine and flushes out all peer information
        /// </summary>
        public WaitHandle[] Stop()
        {
            List<WaitHandle> waitHandles = new List<WaitHandle>(this.torrents.Count);

            for (int i = 0; i < this.torrents.Count; i++)
                if (this.torrents[i].State != TorrentState.Stopped)
                    waitHandles.Add(this.Stop(this.torrents[i]));

            return waitHandles.ToArray();
        }


        /// <summary>
        /// Stops the specified torrent and flushes out all peer information
        /// </summary>
        /// <param name="manager"></param>
        public WaitHandle Stop(TorrentManager manager)
        {
            if (manager.State == TorrentState.Stopped)
                throw new TorrentException("This torrent is already stopped");

            WaitHandle handle = manager.Stop();

            for (int i = 0; i < this.torrents.Count; i++)
                if (this.torrents[i].State != TorrentState.Paused && this.torrents[i].State != TorrentState.Stopped)
                    return handle;              // There's still a torrent running, so just return the handle

            timer.Enabled = false;              // All the torrents are stopped, so stop ticking

            if (this.listener.IsListening)      // Also stop listening for incoming connections
                this.listener.Stop();

            return handle;                      // Now return the handle
        }


        /// <summary>
        /// Stops all torrents in the engine but retains all peer information
        /// </summary>
        public void Pause()
        {
            for (int i = 0; i < this.torrents.Count; i++)
                Pause(this.torrents[i]);
        }


        /// <summary>
        /// Stops the specified torrent but retains all peer information
        /// </summary>
        /// <param name="manager"></param>
        public void Pause(TorrentManager manager)
        {
            manager.Pause();

            for (int i = 0; i < this.torrents.Count; i++)
                if (this.torrents[i].State != TorrentState.Paused && this.torrents[i].State != TorrentState.Stopped)
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
        public TorrentManager LoadTorrent(string path)
        {
            return this.LoadTorrent(path, this.settings.SavePath, this.defaultTorrentSettings);
        }


        /// <summary>
        /// Loads a .torrent file from the specified path
        /// </summary>
        /// <param name="path">The path of the .torrent file</param>
        /// <param name="savePath">The path to download the files to</param>
        /// <returns>A TorrentManager used to control the torrent</returns>
        public TorrentManager LoadTorrent(string path, string savePath)
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
        public TorrentManager LoadTorrent(string path, string savePath, TorrentSettings torrentSettings)
        {
            Torrent torrent = new Torrent();
            try
            {
                torrent.LoadTorrent(path);
            }
            catch (Exception ex)
            {
                throw new TorrentLoadException("Could not load the torrent", ex);
            }

            if (this.ContainsTorrent(BitConverter.ToString(torrent.InfoHash)))
                throw new TorrentException("The torrent is already in the engine");

            TorrentManager manager = new TorrentManager(torrent, savePath, torrentSettings, this.settings);
            this.torrents.Add(manager);

            if (File.Exists(torrent.TorrentPath + ".fresume"))
                if (LoadFastResume(manager))
                    manager.HashChecked = true;

            return (manager);
        }

        private bool ContainsTorrent(string p)
        {
            for (int i = 0; i < this.torrents.Count; i++)
                if (BitConverter.ToString(this.torrents[i].Torrent.InfoHash) == p)
                    return true;

            return false;
        }


        /// <summary>
        /// Loads a .torrent file from the specified URL
        /// </summary>
        /// <param name="url">The URL to download the .torrent from</param>
        /// <param name="location">The path on your computer to download the .torrent to before it's loaded into the engine</param>
        /// <returns></returns>
        public TorrentManager LoadTorrent(Uri url, string location)
        {
            return this.LoadTorrent(url, location, settings.SavePath, this.defaultTorrentSettings);
        }


        /// <summary>
        /// Loads a .torrent file from the specified URL
        /// </summary>
        /// <param name="url">The URL to download the .torrent from</param>
        /// <param name="location">The path on your computer to download the .torrent to before it's loaded into the engine</param>
        /// <param name="savePath">The path to download the files to</param>
        /// <returns></returns>
        public TorrentManager LoadTorrent(Uri url, string location, string savePath)
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
        public TorrentManager LoadTorrent(Uri url, string location, string savePath, TorrentSettings settings)
        {
            WebClient client = new WebClient();
            try
            {
                client.DownloadFile(url, location);
            }
            catch
            {
                throw new TorrentException("Could not download .torrent file from source");
            }
            return this.LoadTorrent(location, savePath, settings);
        }


        /// <summary>
        /// Loads fast resume data if it exists
        /// </summary>
        /// <param name="manager">The manager to load fastresume data for</param>
        /// <returns></returns>
        private bool LoadFastResume(TorrentManager manager)
        {
            try
            {
                XmlSerializer fastResume = new XmlSerializer(typeof(int[]));
                using (FileStream file = File.OpenRead(manager.Torrent.TorrentPath + ".fresume"))
                    manager.PieceManager.MyBitField.FromArray((int[])fastResume.Deserialize(file));

                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Removes the specified torrent from the engine
        /// </summary>
        /// <param name="manager"></param>
        public WaitHandle Remove(TorrentManager manager)
        {
            WaitHandle handle = null;
            if (manager.State != TorrentState.Stopped)
                handle = this.Stop(manager);

            this.torrents.Remove(manager);
            manager.Dispose();
            return handle;
        }


        /// <summary>
        /// Removes all torrents from the Engine
        /// </summary>
        public WaitHandle[] RemoveAll()
        {
            WaitHandle[] handles = new WaitHandle[this.torrents.Count];
            List<TorrentManager> managers = new List<TorrentManager>();

            for (int i = 0; i < torrents.Count; i++)
                managers.Add(this.torrents[i]);

            for (int i = 0; i < managers.Count; i++)
                handles[i] = Remove(managers[i]);

            return handles;
        }
        #endregion


        #region Misc
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static string GeneratePeerId()
        {
            StringBuilder sb = new StringBuilder(20);
            Random rand = new Random((int)DateTime.Now.TimeOfDay.TotalMilliseconds);

            sb.Append(Common.VersionInfo.ClientVersion);
            for (int i = 0; i < 12; i++)
                sb.Append(rand.Next(0, 9));

            return sb.ToString();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogicTick(object sender, ElapsedEventArgs e)
        {
            tickCount++;

            for (int i = 0; i < this.torrents.Count; i++)
            {
                switch (this.torrents[i].State)
                {
                    case (TorrentState.Downloading):
                        this.torrents[i].DownloadLogic(tickCount);
                        break;

                    case (TorrentState.Seeding):
                        this.torrents[i].SeedingLogic(tickCount);
                        break;

                    case (TorrentState.SuperSeeding):
                        this.torrents[i].SuperSeedingLogic(tickCount);
                        break;

                    default:
                        break;  // Do nothing.
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void IncomingConnectionReceived(IAsyncResult result)
        {
            PeerConnectionID id = null;
            try
            {
                Socket peerSocket = ((Socket)result.AsyncState).EndAccept(result);
                if (!peerSocket.Connected)
                    return;

                Peer peer = new Peer(string.Empty, peerSocket.RemoteEndPoint.ToString());
                peer.Connection = new TCPConnection(peerSocket, 0, new NoEncryption());
                id = new PeerConnectionID(peer);
                id.Peer.Connection.ProcessingQueue = true;
                id.Peer.Connection.LastMessageSent = DateTime.Now;
                id.Peer.Connection.LastMessageReceived = DateTime.Now;

                ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, BufferType.SmallMessageBuffer);
                id.Peer.Connection.BytesReceived = 0;
                id.Peer.Connection.BytesToRecieve = 68;
                Logger.Log(id, "CE Peer incoming connection accepted");
                id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, peerHandshakeReceived, id, out id.ErrorCode);
            }
            catch (SocketException)
            {
                if (id != null)
                    this.CleanupSocket(id);
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                if (!this.listener.Disposed)
                    this.listener.BeginAccept();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(IAsyncResult result)
        {
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;

            try
            {
                lock (id)
                {
                    int bytesReceived = id.Peer.Connection.EndReceive(result, out id.ErrorCode);
                    if (bytesReceived == 0)
                    {
                        Logger.Log(id, "CE Recieved 0 for handshake");
                        CleanupSocket(id);
                        return;
                    }

                    id.Peer.Connection.BytesReceived += bytesReceived;
                    if (id.Peer.Connection.BytesReceived != id.Peer.Connection.BytesToRecieve)
                    {
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived, SocketFlags.None, peerHandshakeReceived, id, out id.ErrorCode);
                        return;
                    }
                    Logger.Log(id, "CE Recieved handshake");

                    handleHandshake(id);
                }
            }

            catch (SocketException)
            {
                Logger.Log(id, "CE Exception with handshake");
                CleanupSocket(id);
            }
            catch (NullReferenceException)
            {
                CleanupSocket(id);
            }
        }

        private void handleHandshake(PeerConnectionID id)
        {
            TorrentManager man = null;
            bool handshakeFailed = false;

            HandshakeMessage handshake = new HandshakeMessage();
            try
            {
                handshake.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);
#warning call handshake.Handle to do this properly
                if (handshake.ProtocolString != VersionInfo.ProtocolStringV100)
                    handshakeFailed = true;
            }
            catch
            {
                handshakeFailed = true;
            }

            if (handshakeFailed)
            {
                if (id.Peer.Connection.Encryptor is NoEncryption && SupportsEncryption)
                {
                    // Maybe this was a Message Stream Encryption handshake. Parse it as such.
                    id.Peer.Connection.Encryptor = new PeerBEncryption(Torrents, this.settings.MinEncryptionLevel);
                    id.Peer.Connection.Encryptor.SetPeerConnectionID(id);
                    id.Peer.Connection.Encryptor.onEncryptorReady += onEncryptorReadyHandler;
                    id.Peer.Connection.Encryptor.onEncryptorIOError += onEncryptorIOErrorHandler;
                    id.Peer.Connection.Encryptor.onEncryptorEncryptionError += onEncryptorEncryptionErrorHandler;
                    id.Peer.Connection.StartEncryption(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);
                    return;
                }
                else
                {
                    Logger.Log(id, "CE Invalid handshake");
                    CleanupSocket(id);
                }
                return;
            }

            for (int i = 0; i < this.torrents.Count; i++)
                if (ToolBox.ByteMatch(handshake.infoHash, this.torrents[i].Torrent.InfoHash))
                    man = this.torrents[i];

            if (man == null)        // We're not hosting that torrent
            {
                Logger.Log(id, "CE Not tracking torrent");
                CleanupSocket(id);
                return;
            }

            id.Peer.PeerId = handshake.PeerId;
            id.TorrentManager = man;

            // If the handshake was parsed properly without encryption, then it definitely was not encrypted. If this is not allowed, abort
            if ((id.Peer.Connection.Encryptor is NoEncryption && this.settings.MinEncryptionLevel != EncryptionType.None) && ClientEngine.SupportsEncryption)
            {
                Logger.Log(id, "CE Require crypto");
                CleanupSocket(id);
                return;
            }

            handshake.Handle(id);
            Logger.Log(id, "CE Handshake successful");

            ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);
            id.Peer.Connection.ClientApp = new PeerID(handshake.PeerId);

            handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, ClientEngine.peerId, VersionInfo.ProtocolStringV100);
            BitfieldMessage bf = new BitfieldMessage(id.TorrentManager.Bitfield);

            ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, BufferType.LargeMessageBuffer);
            id.Peer.Connection.BytesSent = 0;
            id.Peer.Connection.BytesToSend = handshake.Encode(id.Peer.Connection.sendBuffer, 0);
            id.Peer.Connection.BytesToSend += bf.Encode(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesToSend);

            Logger.Log(id, "CE Sending to torrent manager");
            id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend, SocketFlags.None, new AsyncCallback(ClientEngine.ConnectionManager.IncomingConnectionAccepted), id, out id.ErrorCode);
            id.Peer.Connection.ProcessingQueue = false;
        }


        private void onEncryptorReady(PeerConnectionID id)
        {
            try
            {
                id.Peer.Connection.BytesReceived = 0;
                id.Peer.Connection.BytesToRecieve = 68;
                Logger.Log(id, "CE Peer encryption handshake complete");
                int bytesReceived = 0;

                // Handshake was probably delivered as initial payload. Retrieve it if its' vailable
                if (id.Peer.Connection.Encryptor.IsInitialDataAvailable())
                    bytesReceived = id.Peer.Connection.Encryptor.GetInitialData(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);

                id.Peer.Connection.BytesReceived += bytesReceived;
                if (id.Peer.Connection.BytesReceived != id.Peer.Connection.BytesToRecieve)
                {
                    id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived, SocketFlags.None, peerHandshakeReceived, id, out id.ErrorCode);
                    return;
                }

                // The complete handshake was in the initial payload
                Logger.Log(id, "CE Recieved Encrypted handshake");

                handleHandshake(id);
            }

            catch (SocketException)
            {
                Logger.Log(id, "CE Exception with handshake");
                CleanupSocket(id);
            }
            catch (NullReferenceException)
            {
                CleanupSocket(id);
            }
        }

        private void onEncryptorError(PeerConnectionID id)
        {
            CleanupSocket(id);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        private void CleanupSocket(PeerConnectionID id)
        {
            if (id == null) // Sometimes onEncryptionError fires with a null id
                return;

            lock (id)
            {
                Logger.Log(id, "***********CE Cleaning up*************");
                if (id.Peer.Connection != null)
                {
                    BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);
                    BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                    id.Peer.Connection.Dispose();
                }
                else
                {
                    Logger.Log(id, "!!!!!!!!!!CE Already null!!!!!!!!");
                }
            }
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
            WaitHandle[] handles = this.RemoveAll();
            for (int i = 0; i < handles.Length; i++)
                if (handles[i] != null)
                    handles[i].WaitOne();

            //foreach (KeyValuePair<string, TorrentManager> keypair in this.torrents)
            //    keypair.Value.Dispose();

            if (!this.listener.Disposed)
                this.listener.Dispose();

            this.timer.Dispose();
            if (this.settings.UsePnP)
                this.portMapper.Dispose();
        }


        public double TotalDownloadSpeed()
        {
            double total = 0;
            for (int i = 0; i < this.torrents.Count; i++)
                total += this.torrents[i].DownloadSpeed();

            return total;
        }


        public double TotalUploadSpeed()
        {
            double total = 0;
            for (int i = 0; i < this.torrents.Count; i++)
                total += this.torrents[i].UploadSpeed();

            return total;
        }
        #endregion
    }
}
