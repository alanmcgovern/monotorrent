using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Encryption;

namespace SampleClient
{
    public class CustomConnection : IConnection
    {
        #region IConnection Members
        private Socket s;
        private bool incoming;
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

        public System.Net.EndPoint EndPoint
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
            return s.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            return s.EndReceive(result);
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return s.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            return s.EndSend(result);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            s.Close();
        }

        #endregion
    }

    public class CustomListner : ConnectionListenerBase
    {
        public override void Dispose()
        {

        }

        public override void Start()
        {

        }

        public override void Stop()
        {

        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            MonoTorrent.Client.Peer p = new MonoTorrent.Client.Peer("", new Uri("tcp://12.123.123.1:2342"), new NoEncryption());
            base.RaiseConnectionReceived(p, connection, manager, true);
        }
    }


    class TestManualConnection
    {
        public TestManualConnection()
        {
            CustomListner listener = new CustomListner();
            ClientEngine engine = new ClientEngine(EngineSettings.DefaultSettings(), listener);
            Torrent t = Torrent.Load(CreateTorrent());
            TorrentManager m = new TorrentManager(t, "", TorrentSettings.DefaultSettings());
            engine.Register(m);
            engine.ConnectionManager.PeerMessageTransferred += delegate(object sender, PeerMessageEventArgs e) { Console.WriteLine(e.Message.ToString()); };
            m.Start();

            TcpListener socketListener = new TcpListener(1220);
            socketListener.Start();
            s1.Connect(IPAddress.Loopback, 1220);
            s2 = socketListener.AcceptSocket();

            CustomConnection c1 = new CustomConnection(s1, true);
            CustomConnection c2 = new CustomConnection(s2, false);
            listener.Add(m, c1);
            listener.Add(m, c2);
            while (true)
            {
                Console.WriteLine("c1 active: {0}", c1.Connected);
                Console.WriteLine("c2 active: {0}", c2.Connected);
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static BEncodedDictionary CreateTorrent()
        {
            BEncodedDictionary infoDict = new BEncodedDictionary();
            infoDict[new BEncodedString("piece length")] = new BEncodedNumber(256 * 1024);
            infoDict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * 15]);
            infoDict[new BEncodedString("length")] = new BEncodedNumber(15 * 256 * 1024 - 1);
            infoDict[new BEncodedString("name")] = new BEncodedString("test.files");

            BEncodedDictionary dict = new BEncodedDictionary();
            dict[new BEncodedString("info")] = infoDict;

            BEncodedList announceTier = new BEncodedList();
            announceTier.Add(new BEncodedString(String.Format("http://transfers/{0}", new byte[20])));
            BEncodedList announceList = new BEncodedList();
            announceList.Add(announceTier);
            dict[new BEncodedString("announce-list")] = announceList;

            return dict;
        }

        Socket s1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Socket s2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

}
