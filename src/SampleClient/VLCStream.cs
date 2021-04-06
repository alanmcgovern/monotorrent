﻿using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;
using System;
using System.Text;

namespace SampleClient
{
    class VLCStream
    {
        ClientEngine Engine { get; }
        string DownloadDirectory { get; }
        Top10Listener Listener { get; }			// This is a subclass of TraceListener which remembers the last 20 statements sent to it

        public VLCStream (ClientEngine engine)
        {
            DownloadDirectory = "streaming_cache";
            Listener = new Top10Listener (10);
            Engine = engine;
        }

        internal async Task StreamAsync (InfoHash infoHash, CancellationToken token)
            => await StreamAsync (await Engine.AddStreamingAsync (new MagnetLink (infoHash), DownloadDirectory), token);

        internal async Task StreamAsync (MagnetLink magnetLink, CancellationToken token)
            => await StreamAsync (await Engine.AddStreamingAsync (magnetLink, DownloadDirectory), token);

        internal async Task StreamAsync (Torrent torrent, CancellationToken token)
            => await StreamAsync (await Engine.AddStreamingAsync (torrent, DownloadDirectory), token);

        async Task StreamAsync (TorrentManager manager, CancellationToken token)
        {
            manager.PeerConnected += (o, e) => {
                lock (Listener)
                    Listener.WriteLine ($"Connection succeeded: {e.Peer.Uri}");
            };
            manager.ConnectionAttemptFailed += (o, e) => {
                lock (Listener)
                    Listener.WriteLine (
                        $"Connection failed: {e.Peer.ConnectionUri} - {e.Reason}");
            };
            // Every time a piece is hashed, this is fired.
            manager.PieceHashed += delegate (object o, PieceHashedEventArgs e) {
                lock (Listener)
                    Listener.WriteLine ($"Piece Hashed: {e.PieceIndex} - {(e.HashPassed ? "Pass" : "Fail")}");
            };

            // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
            manager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e) {
                lock (Listener)
                    Listener.WriteLine ($"OldState: {e.OldState} NewState: {e.NewState}");
            };

            // Every time the tracker's state changes, this is fired
            manager.TrackerManager.AnnounceComplete += (sender, e) => {
                Listener.WriteLine ($"{e.Successful}: {e.Tracker}");
            };

            await manager.StartAsync ();
            PrintStatsAsync (token);
            await manager.WaitForMetadataAsync (token);

            var largestFile = manager.Files.OrderBy (t => t.Length).Last ();
            var stream = await manager.StreamProvider.CreateHttpStreamAsync (largestFile, token);
            var process = Process.Start (@"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe", stream.Uri.ToString ());
            using var disposer = token.Register (process.Kill);
            process.WaitForExit ();
        }

        async void PrintStatsAsync (CancellationToken token)
        {
            try {
                while (!token.IsCancellationRequested) {
                    // Details for all the loaded torrent managers are shown.
                    StringBuilder sb = new StringBuilder (1024);
                    while (Engine.IsRunning) {
                        sb.Remove (0, sb.Length);

                        AppendFormat (sb, $"Transfer Rate:      {Engine.TotalDownloadSpeed / 1024.0:0.00}kB/sec down / {Engine.TotalUploadSpeed / 1024.0:0.00}kB/sec up");
                        AppendFormat (sb, $"Memory Cache:       {Engine.DiskManager.CacheBytesUsed / 1024.0:0.00}/{Engine.Settings.DiskCacheBytes / 1024.0:0.00} kB");
                        AppendFormat (sb, $"Disk IO Rate:       {Engine.DiskManager.ReadRate / 1024.0:0.00} kB/s read / {Engine.DiskManager.WriteRate / 1024.0:0.00} kB/s write");
                        AppendFormat (sb, $"Disk IO Total:      {Engine.DiskManager.TotalBytesRead / 1024.0:0.00} kB read / {Engine.DiskManager.TotalBytesWritten / 1024.0:0.00} kB written");
                        AppendFormat (sb, $"Open Connections:   {Engine.ConnectionManager.OpenConnections}");

                        // Print out the port mappings
                        foreach (var mapping in Engine.PortMappings.Created)
                            AppendFormat (sb, $"Successful Mapping    {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
                        foreach (var mapping in Engine.PortMappings.Failed)
                            AppendFormat (sb, $"Failed mapping:       {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
                        foreach (var mapping in Engine.PortMappings.Pending)
                            AppendFormat (sb, $"Pending mapping:      {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");

                        foreach (TorrentManager manager in Engine.Torrents) {
                            AppendSeparator (sb);
                            AppendFormat (sb, $"State:              {manager.State}");
                            AppendFormat (sb, $"Name:               {(manager.Torrent == null ? "MetaDataMode" : manager.Torrent.Name)}");
                            AppendFormat (sb, $"Progress:           {manager.Progress:0.00}");
                            AppendFormat (sb, $"Transfer Rate:      {manager.Monitor.DownloadSpeed / 1024.0:0.00}kB/s down / {manager.Monitor.UploadSpeed / 1024.0:0.00} kB/s up");
                            AppendFormat (sb, $"Total transferred:  {manager.Monitor.DataBytesDownloaded / (1024.0 * 1024.0):0.00} MB down / {manager.Monitor.DataBytesUploaded / (1024.0 * 1024.0):0.00} MB up");
                            AppendFormat (sb, $"Tracker Status");
                            foreach (var tier in manager.TrackerManager.Tiers)
                                AppendFormat (sb, $"\t{tier.ActiveTracker} : Announce Succeeded: {tier.LastAnnounceSucceeded}. Scrape Succeeded: {tier.LastScrapeSucceeded}.");

                            if (manager.PieceManager != null)
                                AppendFormat (sb, "Current Requests:   {0}", await manager.PieceManager.CurrentRequestCountAsync ());

                            var peers = await manager.GetPeersAsync ();
                            AppendFormat (sb, "Outgoing:");
                            foreach (PeerId p in peers.Where (t => t.ConnectionDirection == Direction.Outgoing)) {
                                AppendFormat (sb, "\t{2} - {1:0.00}/{3:0.00}kB/sec - {0} - {4} ({5})", p.Uri,
                                                                                            p.Monitor.DownloadSpeed / 1024.0,
                                                                                            p.AmRequestingPiecesCount,
                                                                                            p.Monitor.UploadSpeed / 1024.0,
                                                                                            p.EncryptionType,
                                                                                            string.Join ("|", p.SupportedEncryptionTypes.Select (t => t.ToString ()).ToArray ()));
                            }
                            AppendFormat (sb, "");
                            AppendFormat (sb, "Incoming:");
                            foreach (PeerId p in peers.Where (t => t.ConnectionDirection == Direction.Incoming)) {
                                AppendFormat (sb, "\t{2} - {1:0.00}/{3:0.00}kB/sec - {0} - {4} ({5})", p.Uri,
                                                                                            p.Monitor.DownloadSpeed / 1024.0,
                                                                                            p.AmRequestingPiecesCount,
                                                                                            p.Monitor.UploadSpeed / 1024.0,
                                                                                            p.EncryptionType,
                                                                                            string.Join ("|", p.SupportedEncryptionTypes.Select (t => t.ToString ()).ToArray ()));
                            }

                            AppendFormat (sb, "", null);
                            if (manager.Torrent != null)
                                foreach (var file in manager.Files)
                                    AppendFormat (sb, "{1:0.00}% - {0}", file.Path, file.BitField.PercentComplete);
                        }
                        Console.Clear ();
                        Console.WriteLine (sb.ToString ());
                        Listener.ExportTo (Console.Out);

                        await Task.Delay (5000, token);
                    }
                }
            } catch (OperationCanceledException) {

            }
        }

        void AppendSeparator (StringBuilder sb)
        {
            AppendFormat (sb, "", null);
            AppendFormat (sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -", null);
            AppendFormat (sb, "", null);
        }

        void AppendFormat (StringBuilder sb, string str, params object[] formatting)
        {
            if (formatting != null)
                sb.AppendFormat (str, formatting);
            else
                sb.Append (str);
            sb.AppendLine ();
        }
    }
}