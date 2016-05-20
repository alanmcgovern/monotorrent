using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;

namespace SampleClient
{
    public class EngineTestRig
    {
        static EngineTestRig()
        {
            TrackerFactory.Register("custom", typeof(CustomTracker));
        }

        public EngineTestRig(string savePath)
            : this(savePath, 256*1024, null)
        {
        }

        public EngineTestRig(string savePath, PieceWriter writer)
            : this(savePath, 256*1024, writer)
        {
        }

        public EngineTestRig(string savePath, int piecelength, PieceWriter writer)
        {
            if (writer == null)
                writer = new MemoryWriter(new NullWriter());
            Listener = new CustomListener();
            Engine = new ClientEngine(new EngineSettings(), Listener, writer);
            TorrentDict = CreateTorrent(piecelength);
            Torrent = Torrent.Load(TorrentDict);
            Manager = new TorrentManager(Torrent, savePath, new TorrentSettings());
            Engine.Register(Manager);
            //manager.Start();
        }

        public ClientEngine Engine { get; }

        public CustomListener Listener { get; }

        public TorrentManager Manager { get; }

        public Torrent Torrent { get; }

        public BEncodedDictionary TorrentDict { get; }

        public CustomTracker Tracker
        {
            get { return (CustomTracker) Manager.TrackerManager.CurrentTracker; }
        }

        public void AddConnection(IConnection connection)
        {
            Listener.Add(Manager, connection);
        }

        private static BEncodedDictionary CreateTorrent(int pieceLength)
        {
            var infoDict = new BEncodedDictionary();
            infoDict[new BEncodedString("piece length")] = new BEncodedNumber(pieceLength);
            infoDict[new BEncodedString("pieces")] = new BEncodedString(new byte[20*15]);
            infoDict[new BEncodedString("length")] = new BEncodedNumber(15*256*1024 - 1);
            infoDict[new BEncodedString("name")] = new BEncodedString("test.files");

            var dict = new BEncodedDictionary();
            dict[new BEncodedString("info")] = infoDict;

            var announceTier = new BEncodedList();
            announceTier.Add(new BEncodedString("custom://transfers1/announce"));
            announceTier.Add(new BEncodedString("custom://transfers2/announce"));
            announceTier.Add(new BEncodedString("http://transfers3/announce"));
            var announceList = new BEncodedList();
            announceList.Add(announceTier);
            dict[new BEncodedString("announce-list")] = announceList;
            return dict;
        }
    }
}