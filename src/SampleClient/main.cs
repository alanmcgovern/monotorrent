using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;

namespace SampleClient
{
    class MainClass
    {
        static void Main (string[] args)
        {

            var otherHashes = new[] {
                "C4-77-28-49-FE-35-CF-12-B3-60-FF-DC-62-F4-BF-6C-D2-2B-94-A4-4A-93-5A-CA-65-47-AC-65-65-4E-90-FD",
                "72-94-44-0D-E5-B0-D2-C7-33-70-12-D4-9D-7F-A8-BB-17-BF-CA-90-6C-D5-1B-6C-67-FF-DE-97-E0-FD-F6-F2",
                "45-E6-0B-03-A7-3C-FF-19-77-E1-01-7B-FB-00-55-1A-AE-A3-C1-6A-5B-34-22-20-3D-E7-AD-1D-5A-0D-E8-EC",
                "AC-58-29-2E-D6-33-B1-4E-14-EF-CE-F6-B4-EE-31-80-22-1F-41-C6-3D-3B-CE-DA-A4-7A-68-FE-A2-AC-CB-D2",
                "D4-94-61-C3-0A-CD-8F-EB-8E-8F-F6-6D-DE-AD-DC-63-36-08-58-97-61-C3-FF-FF-86-C2-00-27-A5-F8-27-F0"
            }.Select (t => t.Split ('-').Select (t => Convert.ToByte (t, 16)).ToArray ()).ToArray ();

            var otherFirst = SHA256.Create ().ComputeHash (otherHashes[0].Concat (otherHashes[1]).ToArray ());
            var otherSecond = SHA256.Create ().ComputeHash (otherHashes[2].Concat (otherHashes[3]).ToArray ());

            var otherThird = SHA256.Create ().ComputeHash (otherFirst.Concat (otherSecond).ToArray ());

            var str1 = BitConverter.ToString (otherFirst);
            var str2 = BitConverter.ToString (otherSecond);
            var str3 = BitConverter.ToString (otherThird);

            var str4 = BitConverter.ToString (SHA256.Create ().ComputeHash (otherThird.Concat (otherHashes[4]).ToArray ()));

            // hash the first 2 pairs, then hash those togther, then hash each of the remainder.
            var hashes = new[] {
                "19-FC-7C-82-00-41-6B-8C-F9-BF-69-90-98-5B-58-2F-E6-E1-20-1F-CD-FA-72-BB-45-4D-02-E3-E6-EB-DA-8C",
                "ED-97-DB-D8-3B-49-D6-7F-77-3C-D7-FF-93-54-E4-7B-54-5B-6E-81-14-50-88-D2-41-53-62-A5-5A-CF-72-89",
                "CD-91-80-13-B4-DF-9B-4D-9D-76-FE-F4-CD-C3-33-70-A4-3A-1D-96-DD-FB-22-AB-F5-C9-3E-EC-80-B2-9E-D7",
                "9B-02-34-76-02-28-B8-6D-00-C1-20-BB-16-67-EC-3D-95-5A-78-58-AC-BB-0E-A7-CB-7C-64-FD-78-8F-49-89",
                "D8-01-C8-23-78-4F-A8-58-38-D1-F4-5F-B8-34-72-24-47-89-32-AE-EE-4D-97-41-3D-86-3E-B5-E1-A8-B6-A9",
                "E3-8B-0C-EF-4A-C6-42-BD-34-7E-A0-48-ED-33-50-04-27-01-89-35-4F-F6-F1-06-13-47-7C-31-71-96-63-8B",
                "21-4F-2F-EA-68-94-25-B8-23-AF-63-3C-97-25-27-40-9F-72-57-EC-F4-66-73-1D-19-62-BE-57-70-A1-88-E2",
                "72-97-FF-F2-7F-C4-63-29-AC-F0-DD-87-79-5C-A5-32-8C-61-A2-33-6A-C2-A3-BE-4E-34-F0-44-92-AA-46-D9",
                "66-5A-BB-F5-86-C9-5D-0B-A7-85-CC-09-26-EA-EF-CF-38-35-BD-DB-71-A0-BA-30-3A-00-70-20-D2-18-48-DF",
                "7C-28-B8-10-83-29-DA-66-68-D8-ED-F2-EA-1B-FE-2E-D5-8B-10-52-C3-0C-F6-03-19-52-32-78-0B-2E-ED-D6"
            }.Select (t => t.Split ('-').Select (t => Convert.ToByte(t, 16)).ToArray ()).ToArray ();

            var first = SHA256.Create ().ComputeHash (hashes[0].Concat (hashes[1]).ToArray ());
            var second = SHA256.Create ().ComputeHash (hashes[2].Concat (hashes[3]).ToArray ());

            var third = SHA256.Create ().ComputeHash (first.Concat (second).ToArray ());

            var current = third;
            for (int i = 4; i < hashes.Length; i ++) {
                current = SHA256.Create ().ComputeHash (current.Concat (hashes[i]).ToArray ());
            }
            var str = BitConverter.ToString (current);

            CancellationTokenSource cancellation = new CancellationTokenSource ();

            var task = MainAsync (args, cancellation.Token);

            // We need to cleanup correctly when the user closes the window by using ctrl-c
            // or an unhandled exception happens
            Console.CancelKeyPress += delegate { cancellation.Cancel (); task.Wait (); };
            AppDomain.CurrentDomain.ProcessExit += delegate { cancellation.Cancel (); task.Wait (); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine (e.ExceptionObject); cancellation.Cancel (); task.Wait (); };
            Thread.GetDomain ().UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine (e.ExceptionObject); cancellation.Cancel (); task.Wait (); };

            task.Wait ();
        }

        static async Task MainAsync (string[] args, CancellationToken token)
        {
            // Give an example of how settings can be modified for the engine.
            var settingBuilder = new EngineSettingsBuilder {
                // Allow the engine to automatically forward ports using upnp/nat-pmp (if a compatible router is available)
                AllowPortForwarding = true,

                // Automatically save a cache of the DHT table when all torrents are stopped.
                AutoSaveLoadDhtCache = true,

                // Automatically save 'FastResume' data when TorrentManager.StopAsync is invoked, automatically load it
                // before hash checking the torrent. Fast Resume data will be loaded as part of 'engine.AddAsync' if
                // torrent metadata is available. Otherwise, if a magnetlink is used to download a torrent, fast resume
                // data will be loaded after the metadata has been downloaded. 
                AutoSaveLoadFastResume = true,

                // If a MagnetLink is used to download a torrent, the engine will try to load a copy of the metadata
                // it's cache directory. Otherwise the metadata will be downloaded and stored in the cache directory
                // so it can be reloaded later.
                AutoSaveLoadMagnetLinkMetadata = true,

                // Use a fixed port to accept incoming connections from other peers for testing purposes. Production usages should use a random port, 0, if possible.
                ListenEndPoint = new IPEndPoint (IPAddress.Any, 55123),

                // Use a fixed port for DHT communications for testing purposes. Production usages should use a random port, 0, if possible.
                DhtEndPoint = new IPEndPoint (IPAddress.Any, 55123),
            };
            using var engine = new ClientEngine (settingBuilder.ToSettings ());

            Task task;
            if (args.Length == 1 && args[0] == "--vlc") {
                task = new VLCStream (engine).StreamAsync (InfoHash.FromHex ("AEE0F0082CC2F449412C1DD8AF4C58D9AAEE4B5C"), token);
            } else if (args.Length == 1 && MagnetLink.TryParse (args[0], out MagnetLink link)) {
                task = new MagnetLinkStreaming (engine).DownloadAsync (link, token);
            } else {
                task = new StandardDownloader (engine).DownloadAsync (token);
            }

            if (engine.Settings.AllowPortForwarding)
                Console.WriteLine ("uPnP or NAT-PMP port mappings will be created for any ports needed by MonoTorrent");

            try {
                await task;
            } catch (OperationCanceledException) {

            }

            foreach (var manager in engine.Torrents) {
                var stoppingTask = manager.StopAsync ();
                while (manager.State != TorrentState.Stopped) {
                    Console.WriteLine ("{0} is {1}", manager.Torrent.Name, manager.State);
                    await Task.WhenAll (stoppingTask, Task.Delay (250));
                }
                await stoppingTask;
                if (engine.Settings.AutoSaveLoadFastResume)
                    Console.WriteLine ($"FastResume data for {manager.Torrent?.Name ?? manager.InfoHash.ToHex ()} has been written to disk.");
            }

            if (engine.Settings.AutoSaveLoadDhtCache)
                Console.WriteLine ($"DHT cache has been written to disk.");

            if (engine.Settings.AllowPortForwarding)
                Console.WriteLine ("uPnP and NAT-PMP port mappings have been removed");
        }
    }
}
