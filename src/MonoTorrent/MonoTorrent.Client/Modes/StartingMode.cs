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
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    class StartingMode : Mode
    {
        public override bool CanHashCheck => true;
        public override TorrentState State => TorrentState.Starting;

        public Task StartingTask { get; private set; }

        public StartingMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings)
            : base (manager, diskManager, connectionManager, settings)
        {
            CanAcceptConnections = false;
        }

        public override void Tick (int counter)
        {
            if (StartingTask == null)
                StartingTask = WaitForStartingToComplete ();
        }

        public async Task WaitForStartingToComplete ()
        {
            if (!Manager.HasMetadata)
                throw new TorrentException ("Torrents with no metadata must use 'MetadataMode', not 'StartingMode'.");

            try {
                await VerifyHashState ();
            } catch (Exception ex) {
                Manager.TrySetError (Reason.ReadFailure, ex);
                return;
            }

            // If the torrent has not been hashed, we start the hashing process then we wait for it to finish
            // before attempting to start again
            if (!Manager.HashChecked)
            {
                // Deliberately do not wait for the entire hash check to complete in this scenario.
                // Here we want to Task returned by this method to be 'Complete' as soon as the
                // TorrentManager moves to any state that is not Stopped. The idea is that 'StartAsync'
                // will simply kick off 'Hashing' mode, or 'MetadataMode', or 'InitialSeeding' mode
                // and then the user is free to call StopAsync etc whenever they want.
                if (State != TorrentState.Hashing)
                    await Manager.HashCheckAsync(false, false);
            }

            SendAnnounces ();

            if (Manager.Complete && Manager.Settings.InitialSeedingEnabled && ClientEngine.SupportsInitialSeed) {
                Manager.Mode = new InitialSeedingMode(Manager, DiskManager, ConnectionManager, Settings);
            }
            else {
                Manager.Mode = new DownloadMode(Manager, DiskManager, ConnectionManager, Settings);
            }

            Manager.DhtAnnounce();
            await Manager.LocalPeerAnnounceAsync ();

            Manager.PieceManager.Reset();
        }

        async void SendAnnounces ()
        {
            try {
                // We need to announce before going into Downloading mode, otherwise we will
                // send a regular announce instead of a 'Started' announce.
                await Task.WhenAll (
                    Manager.TrackerManager.Scrape (),
                    Manager.TrackerManager.Announce(TorrentEvent.Started)
                );
            } catch {
                // Ignore
            }
        }

        async Task VerifyHashState ()
        {
            // FIXME: I should really just ensure that zero length files always exist on disk. If the first file is
            // a zero length file and someone deletes it after the first piece has been written to disk, it will
            // never be recreated. If the downloaded data requires this file to exist, we have an issue.
            if (Manager.HasMetadata) {
                foreach (var file in Manager.Torrent.Files)
                    if (!file.BitField.AllFalse && Manager.HashChecked && file.Length > 0)
                        Manager.HashChecked &= await DiskManager.CheckFileExistsAsync (file);
            }
        }

    }
}
