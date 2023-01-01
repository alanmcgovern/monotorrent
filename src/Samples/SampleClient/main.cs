using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;

namespace ClientSample
{
    class MainClass
    {
        static void Main (string[] args)
        {
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
            const int httpListeningPort = 55125;
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
                ListenEndPoints = new Dictionary<string, IPEndPoint> {
                    { "ipv4", new IPEndPoint (IPAddress.Any, 55123) },
                    { "ipv6", new IPEndPoint (IPAddress.IPv6Any, 55123) }
                },

                // Use a fixed port for DHT communications for testing purposes. Production usages should use a random port, 0, if possible.
                DhtEndPoint = new IPEndPoint (IPAddress.Any, 55123),


                // Wildcards such as these are supported as long as the underlying .NET framework version, and the operating system, supports them:
                //HttpStreamingPrefix = $"http://+:{httpListeningPort}/"
                //HttpStreamingPrefix = $"http://*.mydomain.com:{httpListeningPort}/"

                // For now just bind to localhost.
                HttpStreamingPrefix = $"http://127.0.0.1:{httpListeningPort}/"
            };
            using var engine = new ClientEngine (settingBuilder.ToSettings ());

            Task task;
            if (args.Length == 1 && args[0] == "--vlc") {
                task = new VLCStream (engine, $"http://127.0.0.1:{httpListeningPort}/").StreamAsync (InfoHash.FromHex ("AEE0F0082CC2F449412C1DD8AF4C58D9AAEE4B5C"), token);
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
                    Console.WriteLine ($"FastResume data for {manager.Torrent?.Name ?? manager.InfoHashes.V1?.ToHex () ?? manager.InfoHashes.V2?.ToHex ()} has been written to disk.");
            }

            if (engine.Settings.AutoSaveLoadDhtCache)
                Console.WriteLine ($"DHT cache has been written to disk.");

            if (engine.Settings.AllowPortForwarding)
                Console.WriteLine ("uPnP and NAT-PMP port mappings have been removed");
        }
    }
}
