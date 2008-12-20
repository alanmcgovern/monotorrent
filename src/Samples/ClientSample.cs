using System;
using System.Collections.Generic;
using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;
using System.IO;
using MonoTorrent.Common;
using System.Net;

namespace Samples
{
    public class ClientSample
    {
        BanList banlist;
        ClientEngine engine;
        List<TorrentManager> managers = new List<TorrentManager>();

        public ClientSample()
        {
            SetupEngine();
            SetupBanlist();
            LoadTorrent();
            StartTorrents();
        }

        void SetupEngine()
        {
            EngineSettings settings = new EngineSettings();
            settings.AllowedEncryption = ChooseEncryption();

            // If both encrypted and unencrypted connections are supported, an encrypted connection will be attempted
            // first if this is true. Otherwise an unencrypted connection will be attempted first.
            settings.PreferEncryption = true;

            // Torrents will be downloaded here by default when they are registered with the engine
            settings.SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Torrents");

            // The maximum upload speed is 200 kilobytes per second, or 204,800 bytes per second
            settings.GlobalMaxUploadSpeed = 200 * 1024;

            engine = new ClientEngine(settings);

            // Tell the engine to listen at port 6969 for incoming connections
            engine.ChangeListenEndpoint(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6969));
        }

        EncryptionTypes ChooseEncryption()
        {
            EncryptionTypes encryption;
            // This completely disables connections - encrypted connections are not allowed
            // and unencrypted connections are not allowed
            encryption = EncryptionTypes.None;

            // Only unencrypted connections are allowed
            encryption = EncryptionTypes.PlainText;

            // Allow only encrypted connections
            encryption = EncryptionTypes.RC4Full | EncryptionTypes.RC4Header;

            // Allow unencrypted and encrypted connections
            encryption = EncryptionTypes.All;
            encryption = EncryptionTypes.PlainText | EncryptionTypes.RC4Full | EncryptionTypes.RC4Header;

            return encryption;
        }

        void SetupBanlist()
        {
            banlist = new BanList();

            if (!File.Exists ("banlist"))
                return;

            // The banlist parser can parse a standard block list from peerguardian or similar services
            BanListParser parser = new BanListParser();
            IEnumerable<AddressRange> ranges = parser.Parse(File.OpenRead("banlist"));
            banlist.AddRange(ranges);

            // Add a few IPAddress by hand
            banlist.Add(IPAddress.Parse("12.21.12.21"));
            banlist.Add(IPAddress.Parse("11.22.33.44"));
            banlist.Add(IPAddress.Parse("44.55.66.77"));

            engine.ConnectionManager.BanPeer += delegate (object o, AttemptConnectionEventArgs e){
                IPAddress address;

                // The engine can raise this event simultaenously on multiple threads
                if (IPAddress.TryParse(e.Peer.ConnectionUri.Host, out address)) {
                    lock (banlist) {
                        // If the value of e.BanPeer is true when the event completes,
                        // the connection will be closed. Otherwise it will be allowed
                        e.BanPeer = banlist.IsBanned(address);
                    }
                }
            };
        }

        void LoadTorrent()
        {
            // Load a .torrent file into memory
            Torrent torrent = Torrent.Load("myfile.torrent");
            
            // Set all the files to not download
            foreach (TorrentFile file in torrent.Files)
                file.Priority = Priority.DoNotDownload;

            // Set the first file as high priority and the second one as normal
            torrent.Files[0].Priority = Priority.Highest;
            torrent.Files[1].Priority = Priority.Normal;

            TorrentManager manager = new TorrentManager(torrent, "DownloadFolder", new TorrentSettings(), (FastResume) null);
            managers.Add(manager);
            engine.Register(manager);

            // Disable rarest first and randomised picking - only allow priority based picking (i.e. selective downloading)
            PiecePicker picker = new StandardPicker();
            picker = new PriorityPicker(picker);
            manager.ChangePicker(picker);
        }

        void StartTorrents()
        {
            engine.StartAll();
        }
    }
}
