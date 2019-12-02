//
// TestRig.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client.Tracker;
using ReusableTasks;

namespace MonoTorrent.Client
{
    public class TestWriter : IPieceWriter
    {
        public List<TorrentFile> FilesThatExist = new List<TorrentFile>();
        public List<TorrentFile> DoNotReadFrom = new List<TorrentFile>();
        public bool DontWrite;

        /// <summary>
        /// this is the list of paths we have read from
        /// </summary>
        public List<string> Paths = new List<string>();

        public int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (DoNotReadFrom.Contains(file))
                return 0;

            if (!Paths.Contains(file.FullPath))
                Paths.Add(file.FullPath);

            if ((offset + count) > file.Length)
                throw new ArgumentOutOfRangeException("Tried to read past the end of the file");
            if (!DontWrite)
                for (int i = 0; i < count; i++)
                    buffer[bufferOffset + i] = (byte)(bufferOffset + i);
            return count;
        }

        public void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {

        }

        public void Close(TorrentFile file)
        {

        }

        public void Dispose ()
        {

        }

        public void Flush(TorrentFile file)
        {

        }

        public bool Exists(TorrentFile file)
        {
            return FilesThatExist.Contains(file);
        }

        public void Move(TorrentFile file, string newPath, bool overwrite)
        {
            
        }
    }

    class CustomTracker : MonoTorrent.Client.Tracker.Tracker
    {
        public List<DateTime> AnnouncedAt = new List<DateTime>();
        public List<DateTime> ScrapedAt = new List<DateTime>();

        public bool FailAnnounce;
        public bool FailScrape;

        List<Peer> peers = new List<Peer>();

        public CustomTracker(Uri uri)
            : base(uri)
        {
            CanAnnounce = true;
            CanScrape = true;
        }

        protected override Task<List<Peer>> DoAnnounceAsync(AnnounceParameters parameters)
        {
            AnnouncedAt.Add(DateTime.Now);
            if (FailAnnounce)
                throw new TrackerException ("Deliberately failing announce request", null);

            return Task.FromResult (peers);
        }

        protected override Task DoScrapeAsync(ScrapeParameters parameters)
        {
            ScrapedAt.Add(DateTime.Now);
            if (FailScrape)
                throw new TrackerException ("Deliberately failing scrape request", null);

            return Task.CompletedTask;
        }

        public void AddPeer(Peer peer)
        {
            peers.Add (peer);
        }

        public override string ToString()
        {
            return Uri.ToString();
        }
    }

    public class CustomConnection : IConnection2
    {
        public byte[] AddressBytes => ((IPEndPoint)EndPoint).Address.GetAddressBytes();
        public bool CanReconnect => false;
        public bool Connected  { get; private set; } = true;
        public EndPoint EndPoint => new IPEndPoint (IPAddress.Parse (Uri.Host), Uri.Port);
        public bool IsIncoming { get; }
        public int? ManualBytesReceived { get; set; }
        public int? ManualBytesSent { get; set; }
        public string Name => IsIncoming ? "Incoming" : "Outgoing";
        public bool SlowConnection { get; set; }
        public Uri Uri => new Uri("ipv4://127.0.0.1:1234");

        public List<int> Receives { get; } = new List<int> ();
        public List<int> Sends { get; } = new List<int> ();

        Stream ReadStream { get; }
        Stream WriteStream { get; }

        public CustomConnection(Stream readStream, Stream writeStream, bool isIncoming)
        {
            ReadStream = readStream;
            WriteStream = writeStream;
            IsIncoming = isIncoming;
        }
        
        Task IConnection.ConnectAsync()
            => throw new InvalidOperationException();

        public ReusableTask ConnectAsync()
            => throw new InvalidOperationException();

        public void Dispose()
        {
            ReadStream.Dispose ();
            WriteStream.Dispose ();
            Connected = false;
        }

        async Task<int> IConnection.ReceiveAsync(byte[] buffer, int offset, int count)
            => await ReceiveAsync (buffer, offset, count);

        public async ReusableTask<int> ReceiveAsync(byte[] buffer, int offset, int count)
        {
            if (SlowConnection)
                count = Math.Min(88, count);

            var result = await ReadStream.ReadAsync (buffer, offset, count, CancellationToken.None);
            Receives.Add (result);
            return ManualBytesReceived ?? result;
        }

        async Task<int> IConnection.SendAsync (byte[] buffer, int offset, int count)
            => await SendAsync (buffer, offset, count);

        public async ReusableTask<int> SendAsync(byte[] buffer, int offset, int count)
        {
            if (SlowConnection)
                count = Math.Min(88, count);

            await WriteStream.WriteAsync(buffer, offset, count, CancellationToken.None);
            Sends.Add (count);
            return ManualBytesSent ?? count;
        }

        public override string ToString()
            => Name;
    }

    class CustomListener : IPeerListener
    {
        public event EventHandler<NewConnectionEventArgs> ConnectionReceived;
        public event EventHandler<EventArgs> StatusChanged;

        public ListenerStatus Status { get; private set; }

        public void Start()
        {
            Status = ListenerStatus.Listening;
            StatusChanged?.Invoke (this, EventArgs.Empty);
        }

        public void Stop()
        {
            Status = ListenerStatus.NotListening;
            StatusChanged?.Invoke (this, EventArgs.Empty);
        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            var p = new Peer("", new Uri("ipv4://12.123.123.1:2342"), EncryptionTypes.All);
            ConnectionReceived?.Invoke (this, new NewConnectionEventArgs (p, connection, manager));
        }
    }

    public class ConnectionPair : IDisposable
    {
        static readonly TimeSpan Timeout = System.Diagnostics.Debugger.IsAttached ? TimeSpan.FromHours (1) : TimeSpan.FromSeconds (5);

        IDisposable CancellationRegistration { get; set; }

        public CustomConnection Incoming { get; }
        public CustomConnection Outgoing { get ; }

        public ConnectionPair()
        {
            var incoming = new SocketStream ();
            var outgoing = new SocketStream ();
            Incoming = new CustomConnection(incoming, outgoing, true);
            Outgoing = new CustomConnection(outgoing, incoming, false);
        }

        public void Dispose()
        {
            CancellationRegistration?.Dispose ();
            Incoming.Dispose();
            Outgoing.Dispose();
        }

        public ConnectionPair WithTimeout ()
        {
            CancellationTokenSource cancellation = new CancellationTokenSource (Timeout);
            CancellationRegistration = cancellation.Token.Register (Dispose);
            return this;
        }
    }

    class TestRig : IDisposable
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

        internal CustomTracker Tracker
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
            Peer peer = new Peer(sb.ToString(), new Uri("ipv4://127.0.0.1:" + (port++)));
            PeerId id = new PeerId(peer, NullConnection.Incoming, Manager.Bitfield?.Clone ().SetAll (false));
            id.SupportsFastPeer = supportsFastPeer;
            id.ProcessingQueue = processingQueue;
            return id;
        }

        public void Dispose()
        {
            engine.Dispose();
        }

        public async Task RecreateManager()
        {
            if (manager != null)
            {
                manager.Dispose();
                if (engine.Contains(manager))
                    await engine.Unregister(manager);
            }
            torrentDict = CreateTorrent(piecelength, files, tier);
            torrent = Torrent.Load(torrentDict);
            if (MetadataMode)
                manager = new TorrentManager(torrent.InfoHash, savePath, new TorrentSettings(), MetadataPath, new RawTrackerTiers ());
            else
                manager = new TorrentManager(torrent, savePath, new TorrentSettings());
            await engine.Register(manager);
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
            engine.RegisterLocalPeerDiscovery (new ManualLocalPeerListener ());
            Writer = writer;

            RecreateManager().Wait();
        }

        static TestRig()
        {
            TrackerFactory.Register("custom", uri => new CustomTracker (uri));
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

        static BEncodedDictionary CreateTorrent(int pieceLength, TorrentFile[] files, string[][] tier)
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            BEncodedDictionary infoDict = new BEncodedDictionary();

            if (tier != null)
                AddAnnounces(dict, tier);

            AddFiles(infoDict, files, pieceLength);
            dict["creation date"] = (BEncodedNumber)(int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            dict["encoding"] = (BEncodedString)"UTF-8";
            dict["info"] = infoDict;

            return dict;
        }

        internal static Torrent CreateMultiFileTorrent (TorrentFile [] files, int pieceLength)
        {
            using (var rig = CreateMultiFile (files, pieceLength))
               return rig.Torrent;
        }

        static void AddFiles(BEncodedDictionary dict, TorrentFile[] files, int pieceLength)
        {
            long totalSize = pieceLength - 1;
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
            dict[new BEncodedString("piece length")] = new BEncodedNumber(pieceLength);
            dict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * (totalSize / pieceLength)]);
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

        internal static TorrentManager CreatePrivate ()
        {
            var dict = CreateTorrent (16 * 1024 * 8, new [] { new TorrentFile ("File", 16 * 1024 * 8) } , null);
            var editor = new TorrentEditor (dict) {
                CanEditSecureMetadata = true,
                Private = true,
            };
            return new TorrentManager (editor.ToTorrent (), "", new TorrentSettings ());
        }

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

        internal static TorrentManager CreateSingleFileManager (int torrentSize, int pieceLength)
        {
            return CreateSingleFile (torrentSize, pieceLength, false).Manager;
        }

        internal static TorrentManager CreateMultiFileManager (int[] fileSizes, int pieceLength)
        {
            var files = fileSizes.Select ((size, index) => new TorrentFile ("File " + index, size)).ToArray ();
            return CreateMultiFileManager (files, pieceLength);
        }

        internal static TorrentManager CreateMultiFileManager (TorrentFile[] files, int pieceLength)
        {
            return CreateMultiFile (files, pieceLength).Manager;
        }
    }
}
