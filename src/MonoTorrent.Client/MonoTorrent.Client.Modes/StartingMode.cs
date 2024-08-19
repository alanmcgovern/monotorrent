//
// StartingMode.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2019 Alan McGovern
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Logging;
using MonoTorrent.Messages.Peer;

using ReusableTasks;

namespace MonoTorrent.Client.Modes
{
    class StartingMode : IMode
    {
        static readonly Logger Log = Logger.Create (nameof (StartingMode));

        public bool CanAcceptConnections => false;
        public bool CanHandleMessages => false;
        public bool CanHashCheck => true;
        public TorrentState State => TorrentState.Starting;
        public CancellationToken Token => Cancellation.Token;

        CancellationTokenSource Cancellation { get; }
        ConnectionManager ConnectionManager { get; }
        DiskManager DiskManager { get; }
        TorrentManager Manager { get; }
        EngineSettings Settings { get; }

        public StartingMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings)
           => (Cancellation, Manager, DiskManager, ConnectionManager, Settings) = (new CancellationTokenSource (), manager, diskManager, connectionManager, settings);

        public void Dispose ()
            => Cancellation.Cancel ();

        public void HandleFilePriorityChanged (ITorrentManagerFile file, Priority oldPriority)
        {
            // Nothing
        }

        public void HandleMessage (PeerId id, PeerMessage message, PeerMessage.Releaser releaser)
            => throw new NotSupportedException ();

        public void HandlePeerConnected (PeerId id)
            => throw new NotSupportedException ();

        public void HandlePeerDisconnected (PeerId id)
            => throw new NotSupportedException ();

        public bool ShouldConnect (Peer peer)
            => false;

        public void Tick (int counter)
        {
        }

        public async Task WaitForStartingToComplete ()
        {
            if (!Manager.HasMetadata)
                throw new TorrentException ("Torrents with no metadata must use 'MetadataMode', not 'StartingMode'.");

            try {
                // Run some basic validations to see if we should force a hashcheck
                await VerifyHashState ();

                // Create any files of length zero, and truncate any files which are too long
                await CreateOrTruncateFiles ();
                Cancellation.Token.ThrowIfCancellationRequested ();
                Manager.PieceManager.Initialise ();
            } catch (Exception ex) {
                Cancellation.Token.ThrowIfCancellationRequested ();
                Manager.TrySetError (Reason.ReadFailure, ex);
                return;
            }

            // If the torrent has not been hashed, we start the hashing process then we wait for it to finish
            // before attempting to start again
            if (!Manager.HashChecked) {
                // Deliberately do not wait for the entire hash check to complete in this scenario.
                // Here we want to Task returned by this method to be 'Complete' as soon as the
                // TorrentManager moves to any state that is not Stopped. The idea is that 'StartAsync'
                // will simply kick off 'Hashing' mode, or 'MetadataMode', or 'InitialSeeding' mode
                // and then the user is free to call StopAsync etc whenever they want.
                if (State != TorrentState.Hashing) {
                    // NOTE: 'StartingMode' will be implicitly cancelled by virtue of running a hash check
                    // and the current mode will change from 'StartingMode' to 'HashingMode'. We should only
                    // run the remainder of the 'StartingMode' logic if the HashingMode is not cancelled.
                    await Manager.HashCheckAsync (false, false);
                    Manager.Mode.Token.ThrowIfCancellationRequested ();
                }
            }

            if (!Manager.HashChecked) {
                if (Manager.Mode == this)
                    Manager.Mode = new StoppedMode ();
                return;
            }

            foreach (var peer in Manager.Peers.AvailablePeers)
                peer.MaybeStale = true;

            SendAnnounces ();

            // Save the fast resume data before updating the current mode. This ensures the on-disk data has
            // either been refreshed or deleted before we make a user-visible change to the state of the torrent.
            if (Manager.Complete)
                await Manager.MaybeWriteFastResumeAsync ();
            else
                await Manager.MaybeDeleteFastResumeAsync ();

            Manager.PieceManager.Initialise ();

            if (Manager.PendingV2PieceHashes.TrueCount > 0)
                await TryLoadV2HashesFromCache ();

            if (Manager.PendingV2PieceHashes.TrueCount > 0) {
                Manager.Mode = new PieceHashesMode (Manager, DiskManager, ConnectionManager, Settings, false);
            } else if (Manager.Complete && Manager.Settings.AllowInitialSeeding && ClientEngine.SupportsInitialSeed) {
                Manager.Mode = new InitialSeedingMode (Manager, DiskManager, ConnectionManager, Settings);
            } else {
                Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            }

            await Manager.DhtAnnounceAsync ();
            await Manager.LocalPeerAnnounceAsync ();
        }

        async ReusableTask CreateOrTruncateFiles ()
        {
            foreach (TorrentFileInfo file in Manager.Files.Where (t => t.Priority != Priority.DoNotDownload)) {
                var maybeLength = await DiskManager.GetLengthAsync (file).ConfigureAwait (false);
                // If the file doesn't exist, create it.
                if (!maybeLength.HasValue)
                    await DiskManager.CreateAsync (file, Settings.FileCreationOptions).ConfigureAwait (false);

                // Otherwise if the destination file is larger than it should be, truncate it
                else if (maybeLength.Value > file.Length)
                    await DiskManager.SetLengthAsync (file, file.Length).ConfigureAwait (false);

                if (file.Length == 0)
                    file.BitField[0] = true;
            }

            // Then check if any 'DoNotDownload' file overlaps with a file we are downloading.
            var downloadingFiles = Manager.Files.Where (t => t.Priority != Priority.DoNotDownload).ToArray ();
            foreach (var ignoredFile in Manager.Files.Where (t => t.Priority == Priority.DoNotDownload)) {
                foreach (var downloading in downloadingFiles) {
                    if (ignoredFile.Overlaps (downloading))
                        await DiskManager.CreateAsync (ignoredFile, Settings.FileCreationOptions);
                }
            }

            // After potentially creating or truncating files, refresh the state.
            await Manager.RefreshAllFilesCorrectLengthAsync ();
        }

        async void SendAnnounces ()
        {
            try {
                // We need to announce before going into Downloading mode, otherwise we will
                // send a regular announce instead of a 'Started' announce.
                await Task.WhenAll (
                    Manager.TrackerManager.ScrapeAsync (CancellationToken.None).AsTask (),
                    Manager.TrackerManager.AnnounceAsync (TorrentEvent.Started, CancellationToken.None).AsTask ()
                );
            } catch (Exception ex) {
                Log.Exception (ex, "Error announcing/scraping while starting the torrent");
            }
        }

        async Task TryLoadV2HashesFromCache ()
        {
            try {
                await MainLoop.SwitchThread ();

                var path = Settings.GetV2HashesPath (Manager.InfoHashes);
                if (File.Exists (path)) {
                    var data = BEncodedValue.Decode<BEncodedDictionary> (File.ReadAllBytes (path));
                    Manager.PieceHashes = Manager.Torrent!.CreatePieceHashes (data.ToDictionary (t => MerkleRoot.FromMemory (t.Key.AsMemory ()), kvp => ReadOnlyMerkleTree.FromLayer (Manager.Torrent.PieceLength, ((BEncodedString) kvp.Value).Span)));
                    Manager.PendingV2PieceHashes.SetAll (false);
                }
            } catch (Exception ex) {
                Log.ExceptionFormated (ex, "Could not load hashes for {0}", Manager.Name);
            }
        }

        async Task VerifyHashState ()
        {
            // If we do not have metadata or the torrent needs a hash check, fast exit.
            if (!Manager.HashChecked)
                return;

            // Lightweight check - if any files are missing but we believe data should exist, reset the 'needs hashcheck' boolean
            // so we force a hash check.
            foreach (ITorrentManagerFile file in Manager.Files) {
                if (!file.BitField.AllFalse && file.Length > 0) {
                    if (!await DiskManager.CheckFileExistsAsync (file)) {
                        Manager.SetNeedsHashCheck ();
                        break;
                    }
                }
            }
        }
    }
}
