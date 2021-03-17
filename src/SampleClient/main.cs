using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;

namespace SampleClient
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
            Task task;

            using var engine = new ClientEngine ();

            if (args.Length == 1 && MagnetLink.TryParse (args[0], out MagnetLink link)) {
                task = new MagnetLinkStreaming (engine).DownloadAsync (link, token);
            } else {
                task = new StandardDownloader (engine).DownloadAsync (token);
            }

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
        }
    }
}
