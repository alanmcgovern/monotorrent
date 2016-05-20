using System;
using System.Text;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;

namespace MonoTorrent.Tests.Client
{
    public class TestRig : IDisposable
    {
        private static readonly Random Random = new Random(1000);
        private static int port = 10000;
        private readonly int piecelength;


        private readonly string savePath;
        private readonly string[][] tier;

        public int BlocksPerPiece
        {
            get { return Torrent.PieceLength/(16*1024); }
        }

        public int Pieces
        {
            get { return Torrent.Pieces.Count; }
        }

        public int TotalBlocks
        {
            get
            {
                var count = 0;
                var size = Torrent.Size;
                while (size > 0)
                {
                    count++;
                    size -= Piece.BlockSize;
                }
                return count;
            }
        }

        public TestWriter Writer { get; set; }

        public ClientEngine Engine { get; }

        public CustomListener Listener { get; }

        public TorrentManager Manager { get; private set; }

        public bool MetadataMode { get; }

        public string MetadataPath { get; set; }

        public Torrent Torrent { get; private set; }

        public BEncodedDictionary TorrentDict { get; private set; }

        public CustomTracker Tracker
        {
            get { return (CustomTracker) Manager.TrackerManager.CurrentTracker; }
        }

        public void Dispose()
        {
            Engine.Dispose();
        }

        public void AddConnection(IConnection connection)
        {
            if (connection.IsIncoming)
                Listener.Add(null, connection);
            else
                Listener.Add(Manager, connection);
        }

        public PeerId CreatePeer(bool processingQueue)
        {
            return CreatePeer(processingQueue, true);
        }

        public PeerId CreatePeer(bool processingQueue, bool supportsFastPeer)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < 20; i++)
                sb.Append((char) Random.Next('a', 'z'));
            var peer = new Peer(sb.ToString(), new Uri("tcp://127.0.0.1:" + port++));
            var id = new PeerId(peer, Manager);
            id.SupportsFastPeer = supportsFastPeer;
            id.ProcessingQueue = processingQueue;
            return id;
        }

        public void RecreateManager()
        {
            if (Manager != null)
            {
                Manager.Dispose();
                if (Engine.Contains(Manager))
                    Engine.Unregister(Manager);
            }
            TorrentDict = CreateTorrent(piecelength, files, tier);
            Torrent = Torrent.Load(TorrentDict);
            if (MetadataMode)
                Manager = new TorrentManager(Torrent.infoHash, savePath, new TorrentSettings(), MetadataPath,
                    new RawTrackerTiers());
            else
                Manager = new TorrentManager(Torrent, savePath, new TorrentSettings());
            Engine.Register(Manager);
        }

        internal static TestRig CreateSingleFile(int torrentSize, int pieceLength)
        {
            return CreateSingleFile(torrentSize, pieceLength, false);
        }

        internal static TestRig CreateSingleFile(int torrentSize, int pieceLength, bool metadataMode)
        {
            var files = StandardSingleFile();
            files[0] = new TorrentFile(files[0].Path, torrentSize);
            return new TestRig("", pieceLength, StandardWriter(), StandardTrackers(), files, metadataMode);
        }

        #region Rig Creation

        private readonly TorrentFile[] files;

        private TestRig(string savePath, int piecelength, TestWriter writer, string[][] trackers, TorrentFile[] files)
            : this(savePath, piecelength, writer, trackers, files, false)
        {
        }

        private TestRig(string savePath, int piecelength, TestWriter writer, string[][] trackers, TorrentFile[] files,
            bool metadataMode)
        {
            this.files = files;
            this.savePath = savePath;
            this.piecelength = piecelength;
            tier = trackers;
            MetadataMode = metadataMode;
            MetadataPath = "metadataSave.torrent";
            Listener = new CustomListener();
            Engine = new ClientEngine(new EngineSettings(), Listener, writer);
            Writer = writer;

            RecreateManager();
        }

        static TestRig()
        {
            TrackerFactory.Register("custom", typeof(CustomTracker));
        }

        private static void AddAnnounces(BEncodedDictionary dict, string[][] tiers)
        {
            var announces = new BEncodedList();
            foreach (var tier in tiers)
            {
                var bTier = new BEncodedList();
                announces.Add(bTier);
                foreach (var s in tier)
                    bTier.Add((BEncodedString) s);
            }
            dict["announce"] = (BEncodedString) tiers[0][0];
            dict["announce-list"] = announces;
        }

        private BEncodedDictionary CreateTorrent(int pieceLength, TorrentFile[] files, string[][] tier)
        {
            var dict = new BEncodedDictionary();
            var infoDict = new BEncodedDictionary();

            AddAnnounces(dict, tier);
            AddFiles(infoDict, files);
            if (files.Length == 1)
                dict["url-list"] = (BEncodedString) "http://127.0.0.1:120/announce/File1.exe";
            else
                dict["url-list"] = (BEncodedString) "http://127.0.0.1:120/announce";
            dict["creation date"] = (BEncodedNumber) (int) (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            dict["encoding"] = (BEncodedString) "UTF-8";
            dict["info"] = infoDict;

            return dict;
        }

        private void AddFiles(BEncodedDictionary dict, TorrentFile[] files)
        {
            long totalSize = piecelength - 1;
            var bFiles = new BEncodedList();
            for (var i = 0; i < files.Length; i++)
            {
                var path = new BEncodedList();
                foreach (var s in files[i].Path.Split('/'))
                    path.Add((BEncodedString) s);
                var d = new BEncodedDictionary();
                d["path"] = path;
                d["length"] = (BEncodedNumber) files[i].Length;
                bFiles.Add(d);
                totalSize += files[i].Length;
            }

            dict[new BEncodedString("files")] = bFiles;
            dict[new BEncodedString("name")] = new BEncodedString("test.files");
            dict[new BEncodedString("piece length")] = new BEncodedNumber(piecelength);
            dict[new BEncodedString("pieces")] = new BEncodedString(new byte[20*(totalSize/piecelength)]);
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
            return new TestRig("", StandardPieceSize(), writer, StandardTrackers(), StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(int pieceSize)
        {
            return new TestRig("", pieceSize, StandardWriter(), StandardTrackers(), StandardMultiFile());
        }

        #region Create standard fake data

        private static int StandardPieceSize()
        {
            return 256*1024;
        }

        private static TorrentFile[] StandardMultiFile()
        {
            return new[]
            {
                new TorrentFile("Dir1/File1", (int) (StandardPieceSize()*0.44)),
                new TorrentFile("Dir1/Dir2/File2", (int) (StandardPieceSize()*13.25)),
                new TorrentFile("File3", (int) (StandardPieceSize()*23.68)),
                new TorrentFile("File4", (int) (StandardPieceSize()*2.05))
            };
        }

        private static TorrentFile[] StandardSingleFile()
        {
            return new[]
            {
                new TorrentFile("Dir1/File1", (int) (StandardPieceSize()*0.44))
            };
        }

        private static string[][] StandardTrackers()
        {
            return new[]
            {
                new[] {"custom://tier1/announce1", "custom://tier1/announce2"},
                new[] {"custom://tier2/announce1", "custom://tier2/announce2", "custom://tier2/announce3"}
            };
        }

        private static TestWriter StandardWriter()
        {
            return new TestWriter();
        }

        #endregion Create standard fake data

        #endregion Rig Creation
    }
}