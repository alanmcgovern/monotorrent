using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;

namespace ClientSample
{
    class MagnetLinkStreaming
    {
        ClientEngine Engine { get; }

        public MagnetLinkStreaming (ClientEngine engine)
        {
            Engine = engine;
        }

        public async Task DownloadAsync (MagnetLink link, CancellationToken token)
        {
            var times = new List<(string message, TimeSpan time)> ();
            var manager = await Engine.AddStreamingAsync (link, "downloads");

            var overall = Stopwatch.StartNew ();
            var firstPeerFound = Stopwatch.StartNew ();
            var firstPeerConnected = Stopwatch.StartNew ();
            manager.PeerConnected += (o, e) => {
                if (!firstPeerConnected.IsRunning)
                    return;

                firstPeerConnected.Stop ();
                lock (times)
                    times.Add (("First peer connected. Time since torrent started: ", firstPeerConnected.Elapsed));
            };
            manager.PeersFound += (o, e) => {
                if (!firstPeerFound.IsRunning)
                    return;

                firstPeerFound.Stop ();
                lock (times)
                    times.Add (($"First peers found via {e.GetType ().Name}. Time since torrent started: ", firstPeerFound.Elapsed));
            };
            manager.PieceHashed += (o, e) => {
                if (manager.State != TorrentState.Downloading)
                    return;

                lock (times)
                    times.Add (($"Piece {e.PieceIndex} hashed. Time since torrent started: ", overall.Elapsed));
            };

            await manager.StartAsync ();
            await manager.WaitForMetadataAsync (token);

            var largestFile = manager.Files.OrderByDescending (t => t.Length).First ();
            var stream = await manager.StreamProvider.CreateStreamAsync (largestFile, false);

            // Read the middle
            await TimedRead (manager, stream, stream.Length / 2, times, token);
            // Then the start
            await TimedRead (manager, stream, 0, times, token);
            // Then the last piece
            await TimedRead (manager, stream, stream.Length - 2, times, token);
            // Then the 3rd last piece
            await TimedRead (manager, stream, stream.Length - manager.Torrent.PieceLength * 3, times, token);
            // Then the 5th piece
            await TimedRead (manager, stream, manager.Torrent.PieceLength * 5, times, token);
            // Then 1/3 of the way in
            await TimedRead (manager, stream, stream.Length / 3, times, token);
            // Then 2/3 of the way in
            await TimedRead (manager, stream, stream.Length / 3 * 2, times, token);
            // Then 1/5 of the way in
            await TimedRead (manager, stream, stream.Length / 5, times, token);
            // Then 4/5 of the way in
            await TimedRead (manager, stream, stream.Length / 5 * 4, times, token);

            lock (times) {
                foreach (var (message, time) in times)
                    Console.WriteLine ($"{message} {time.TotalSeconds:0.00} seconds");
            }

            await manager.StopAsync ();
        }

        async Task TimedRead (TorrentManager manager, Stream stream, long position, List<(string, TimeSpan)> times, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew ();
            stream.Seek (position, SeekOrigin.Begin);
            await stream.ReadAsync (new byte[1], 0, 1, token);
            lock (times)
                times.Add (($"Read piece: {manager.Torrent.ByteOffsetToPieceIndex (stream.Position - 1)}. Time since seeking: ", stopwatch.Elapsed));
        }
    }
}
