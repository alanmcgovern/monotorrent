using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Connections;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client;
using MonoTorrent.Common;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Client.Encryption;
using System.Threading;
using NUnit.Framework;

namespace MonoTorrent.Client
{
    public class TestWriter : PieceWriter
    {
        public List<TorrentFile> FilesThatExist = new List<TorrentFile>();
        public List<TorrentFile> DoNotReadFrom = new List<TorrentFile>();
        public bool DontWrite;
        public List<String> Paths = new List<string>();
        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (DoNotReadFrom.Contains(file))
                return 0;

            if (!Paths.Contains(file.FullPath))
                Paths.Add(file.FullPath);

            if (!DontWrite)
                for (int i = 0; i < count; i++)
                    buffer[bufferOffset + i] = (byte)(bufferOffset + i);
            return count;
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {

        }

        public override void Close(TorrentFile file)
        {

        }

        public override void Flush(TorrentFile file)
        {

        }

        public override bool Exists(TorrentFile file)
        {
            return FilesThatExist.Contains(file);
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
            
        }
    }

    public class CustomTracker : MonoTorrent.Client.Tracker.Tracker
    {
        public List<DateTime> AnnouncedAt = new List<DateTime>();
        public List<DateTime> ScrapedAt = new List<DateTime>();

        public bool FailAnnounce;
        public bool FailScrape;

        public CustomTracker(Uri uri)
            : base(uri)
        {
            CanAnnounce = true;
            CanScrape = true;
        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
            RaiseBeforeAnnounce();
            AnnouncedAt.Add(DateTime.Now);
            RaiseAnnounceComplete(new AnnounceResponseEventArgs(this, state, !FailAnnounce));
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
            RaiseBeforeScrape();
            ScrapedAt.Add(DateTime.Now);
            RaiseScrapeComplete(new ScrapeResponseEventArgs(this, state, !FailScrape));
        }

        public void AddPeer(Peer p)
        {
            TrackerConnectionID id = new TrackerConnectionID(this, false, TorrentEvent.None, new ManualResetEvent(false));
            AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(this, id, true);
            e.Peers.Add(p);
            RaiseAnnounceComplete(e);
            Assert.IsTrue(id.WaitHandle.WaitOne(1000, true), "#1 Tracker never raised the AnnounceComplete event");
        }

        public void AddFailedPeer(Peer p)
        {
            TrackerConnectionID id = new TrackerConnectionID(this, true, TorrentEvent.None, new ManualResetEvent(false));
            AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(this, id, false);
            e.Peers.Add(p);
            RaiseAnnounceComplete(e);
            Assert.IsTrue(id.WaitHandle.WaitOne(1000, true), "#2 Tracker never raised the AnnounceComplete event");
        }

        public override string ToString()
        {
            return Uri.ToString();
        }
    }

    public class CustomConnection : IConnection
    {
        public string Name;
        public event EventHandler BeginReceiveStarted;
        public event EventHandler EndReceiveStarted;

        public event EventHandler BeginSendStarted;
        public event EventHandler EndSendStarted;

        private Socket s;
        private bool incoming;

        public int? ManualBytesReceived {
            get; set;
        }

        public int? ManualBytesSent {
            get; set;
        }

        public bool SlowConnection {
            get; set;
        }

        public CustomConnection(Socket s, bool incoming)
        {
            this.s = s;
            this.incoming = incoming;
        }

        public byte[] AddressBytes
        {
            get { return ((IPEndPoint)s.RemoteEndPoint).Address.GetAddressBytes(); }
        }

        public bool Connected
        {
            get { return s.Connected; }
        }

        public bool CanReconnect
        {
            get { return false; }
        }

        public bool IsIncoming
        {
            get { return incoming; }
        }

        public EndPoint EndPoint
        {
            get { return s.RemoteEndPoint; }
        }

        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            throw new InvalidOperationException();
        }

        public void EndConnect(IAsyncResult result)
        {
            throw new InvalidOperationException();
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (BeginReceiveStarted != null)
                BeginReceiveStarted (this, EventArgs.Empty);
            if (SlowConnection)
                count = 1;
            return s.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            if (EndReceiveStarted != null)
                EndReceiveStarted(null, EventArgs.Empty);

            if (ManualBytesReceived.HasValue)
                return ManualBytesReceived.Value;

            try
            {
                return s.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (BeginSendStarted != null)
                BeginSendStarted(null, EventArgs.Empty);

            if (SlowConnection)
                count = 1;
            return s.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            if (EndSendStarted != null)
                EndSendStarted(null, EventArgs.Empty);

            if (ManualBytesSent.HasValue)
                return ManualBytesSent.Value;

            try
            {
                return s.EndSend(result);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }
        //private bool disposed;
        public void Dispose()
        {
           // disposed = true;
            s.Close();
        }

        public override string ToString()
        {
            return Name;
        }

        public Uri Uri
        {
            get { return new Uri("tcp://127.0.0.1:1234"); }
        }


        public int Receive (byte[] buffer, int offset, int count)
        {
            var r = BeginReceive (buffer, offset, count, null, null);
            if (!r.AsyncWaitHandle.WaitOne (TimeSpan.FromSeconds (4)))
                throw new Exception ("Could not receive required data");
            return EndReceive (r);
        }

        public int Send (byte[] buffer, int offset, int count)
        {
            var r = BeginSend (buffer, offset, count, null, null);
            if (!r.AsyncWaitHandle.WaitOne (TimeSpan.FromSeconds (4)))
                throw new Exception ("Could not receive required data");
            return EndSend (r);
        }
    }

    public class CustomListener : PeerListener
    {
        public override void Start()
        {

        }

        public override void Stop()
        {

        }

        public CustomListener()
            :base(new IPEndPoint(IPAddress.Any, 0))
        {
        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            MonoTorrent.Client.Peer p = new MonoTorrent.Client.Peer("", new Uri("tcp://12.123.123.1:2342"), EncryptionTypes.All);
            base.RaiseConnectionReceived(p, connection, manager);
        }
    }

    public class ConnectionPair : IDisposable
    {
        TcpListener socketListener;
        public CustomConnection Incoming;
        public CustomConnection Outgoing;

        public ConnectionPair(int port)
        {
            socketListener = new TcpListener(IPAddress.Loopback, port);
            socketListener.Start();

            Socket s1a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect(IPAddress.Loopback, port);
            Socket s1b = socketListener.AcceptSocket();

            Incoming = new CustomConnection(s1a, true);
            Outgoing = new CustomConnection(s1b, false);
            socketListener.Stop();
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            socketListener.Stop();
        }
    }

    public class TestRig : IDisposable
    {
        static Random Random = new Random(1000);
        static int port = 10000;
        private BEncodedDictionary torrentDict;
        private ClientEngine engine;
        private CustomListener listener;
        private TorrentManager manager;
        private Torrent torrent;

        public int BlocksPerPiece
        {
            get { return torrent.PieceLength / (16 * 1024); }
        }

        public int Pieces
        {
            get { return torrent.Pieces.Count; }
        }

        public int TotalBlocks
        {
            get
            {
                int count = 0;
                long size = torrent.Size;
                while (size > 0)
                {
                    count++;
                    size -= Piece.BlockSize;
                }
                return count;
            }
        }

        public TestWriter Writer {
            get; set;
        }

        public ClientEngine Engine
        {
            get { return engine; }
        }

        public CustomListener Listener
        {
            get { return listener; }
        }

        public TorrentManager Manager
        {
            get { return manager; }
        }

        public bool MetadataMode {
            get; private set;
        }

        public string MetadataPath {
            get; set;
        }

        public Torrent Torrent
        {
            get { return torrent; }
        }

        public BEncodedDictionary TorrentDict
        {
            get { return torrentDict; }
        }

        public CustomTracker Tracker
        {
            get { return (CustomTracker)this.manager.TrackerManager.CurrentTracker; }
        }


        string savePath; int piecelength; string[][] tier;

        public void AddConnection(IConnection connection)
        {
            if (connection.IsIncoming)
                listener.Add(null, connection);
            else
                listener.Add(manager, connection);
        }
        public PeerId CreatePeer(bool processingQueue)
        {
            return CreatePeer(processingQueue, true);
        }

        public PeerId CreatePeer(bool processingQueue, bool supportsFastPeer)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 20; i++)
                sb.Append((char)Random.Next((int)'a', (int)'z'));
            Peer peer = new Peer(sb.ToString(), new Uri("tcp://127.0.0.1:" + (port++)));
            PeerId id = new PeerId(peer, Manager);
            id.SupportsFastPeer = supportsFastPeer;
            id.ProcessingQueue = processingQueue;
            return id;
        }

        public void Dispose()
        {
            engine.Dispose();
        }

        public void RecreateManager()
        {
            if (manager != null)
            {
                manager.Dispose();
                if (engine.Contains(manager))
                    engine.Unregister(manager);
            }
            torrentDict = CreateTorrent(piecelength, files, tier);
            torrent = Torrent.Load(torrentDict);
            if (MetadataMode)
                manager = new TorrentManager(torrent.infoHash, savePath, new TorrentSettings(), MetadataPath, new RawTrackerTiers ());
            else
                manager = new TorrentManager(torrent, savePath, new TorrentSettings());
            engine.Register(manager);
        }

        #region Rig Creation

        TorrentFile[] files;
        TestRig(string savePath, int piecelength, TestWriter writer, string[][] trackers, TorrentFile[] files)
            : this (savePath, piecelength, writer, trackers, files, false)
        {
            
        }

        TestRig(string savePath, int piecelength, TestWriter writer, string[][] trackers, TorrentFile[] files, bool metadataMode)
        {
            this.files = files;
            this.savePath = savePath;
            this.piecelength = piecelength;
            this.tier = trackers;
            MetadataMode = metadataMode;
            MetadataPath = "metadataSave.torrent";
            listener = new CustomListener();
            engine = new ClientEngine(new EngineSettings(), listener, writer);
            Writer = writer;

            RecreateManager();
        }

        static TestRig()
        {
            TrackerFactory.Register("custom", typeof(CustomTracker));
        }

        private static void AddAnnounces(BEncodedDictionary dict, string[][] tiers)
        {
            BEncodedList announces = new BEncodedList();
            foreach (string[] tier in tiers)
            {
                BEncodedList bTier = new BEncodedList();
                announces.Add(bTier);
                foreach (string s in tier)
                    bTier.Add((BEncodedString)s);
            }
            dict["announce"] = (BEncodedString)tiers[0][0];
            dict["announce-list"] = announces;
        }

        BEncodedDictionary CreateTorrent(int pieceLength, TorrentFile[] files, string[][] tier)
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            BEncodedDictionary infoDict = new BEncodedDictionary();

            AddAnnounces(dict, tier);
            AddFiles(infoDict, files);
            if (files.Length == 1)
                dict["url-list"] = (BEncodedString)"http://127.0.0.1:120/announce/File1.exe";
            else
                dict["url-list"] = (BEncodedString)"http://127.0.0.1:120/announce";
            dict["creation date"] = (BEncodedNumber)(int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            dict["encoding"] = (BEncodedString)"UTF-8";
            dict["info"] = infoDict;

            return dict;
        }

        void AddFiles(BEncodedDictionary dict, TorrentFile[] files)
        {
            long totalSize = piecelength - 1;
            BEncodedList bFiles = new BEncodedList();
            for (int i = 0; i < files.Length; i++)
            {
                BEncodedList path = new BEncodedList();
                foreach (string s in files[i].Path.Split('/'))
                    path.Add((BEncodedString)s);
                BEncodedDictionary d = new BEncodedDictionary();
                d["path"] = path;
                d["length"] = (BEncodedNumber)files[i].Length;
                bFiles.Add(d);
                totalSize += files[i].Length;
            }

            dict[new BEncodedString("files")] = bFiles;
            dict[new BEncodedString("name")] = new BEncodedString("test.files");
            dict[new BEncodedString("piece length")] = new BEncodedNumber(piecelength);
            dict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * (totalSize / piecelength)]);
        }

        public static TestRig CreateSingleFile()
        {
            return new TestRig("", StandardPieceSize(), StandardWriter(), StandardTrackers(), StandardSingleFile());
        }

        public static TestRig CreateMultiFile()
        {
            return new TestRig("", StandardPieceSize(), StandardWriter(), StandardTrackers(), StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(TorrentFile[] files, int pieceLength)
        {
            return new TestRig("", pieceLength, StandardWriter(), StandardTrackers(), files);
        }

        public static TestRig CreateTrackers(string[][] tier)
        {
            return new TestRig("", StandardPieceSize(), StandardWriter(), tier, StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(TestWriter writer)
        {
            return new TestRig ("", StandardPieceSize (), writer, StandardTrackers (), StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(int pieceSize)
        {
            return new TestRig("", pieceSize, StandardWriter(), StandardTrackers(), StandardMultiFile());
        }

        #region Create standard fake data

        static int StandardPieceSize()
        {
            return 256 * 1024;
        }

        static TorrentFile[] StandardMultiFile()
        {
            return new TorrentFile[] {
                new TorrentFile ("Dir1/File1", (int)(StandardPieceSize () * 0.44)),
                new TorrentFile ("Dir1/Dir2/File2", (int)(StandardPieceSize () * 13.25)),
                new TorrentFile ("File3", (int)(StandardPieceSize () * 23.68)),
                new TorrentFile ("File4", (int)(StandardPieceSize () * 2.05)),
            };
        }

        static TorrentFile[] StandardSingleFile()
        {
            return new TorrentFile[] {
                 new TorrentFile ("Dir1/File1", (int)(StandardPieceSize () * 0.44))
            };
        }

        static string[][] StandardTrackers()
        {
            return new string[][] {
                new string[] { "custom://tier1/announce1", "custom://tier1/announce2" },
                new string[] { "custom://tier2/announce1", "custom://tier2/announce2", "custom://tier2/announce3" },
            };
        }

        static TestWriter StandardWriter()
        {
            return new TestWriter();
        }

        #endregion Create standard fake data

        #endregion Rig Creation

        internal static TestRig CreateSingleFile(int torrentSize, int pieceLength)
        {
            return CreateSingleFile(torrentSize, pieceLength, false);
        }

        internal static TestRig CreateSingleFile(int torrentSize, int pieceLength, bool metadataMode)
        {
            TorrentFile[] files = StandardSingleFile();
            files[0] = new TorrentFile (files[0].Path, torrentSize);
            return new TestRig("", pieceLength, StandardWriter(), StandardTrackers(), files, metadataMode);
        }
    }
}
