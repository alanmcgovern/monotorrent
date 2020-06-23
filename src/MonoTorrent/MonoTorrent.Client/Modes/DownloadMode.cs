//
// DownloadMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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


using System.Threading;

namespace MonoTorrent.Client.Modes
{
    class DownloadMode : Mode
    {
        TorrentState state;
        public override TorrentState State => state;

        public DownloadMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings)
            : base (manager, diskManager, connectionManager, settings)
        {
            manager.HashFails = 0;

            // Ensure the state is correct. We should either be downloading or seeding based on
            // the files whose priority is not set to 'DoNotDownload'.
            state = manager.Complete ? TorrentState.Seeding : TorrentState.Downloading;
            UpdateSeedingDownloadingState ();
        }

        public override void HandlePeerConnected (PeerId id)
        {
            if (!ShouldConnect (id))
                ConnectionManager.CleanupSocket (Manager, id);
            base.HandlePeerConnected (id);
        }

        public override bool ShouldConnect (Peer peer)
        {
            return !(peer.IsSeeder && Manager.HasMetadata && Manager.Complete);
        }

        public override void Tick (int counter)
        {
            base.Tick (counter);

            UpdateSeedingDownloadingState ();

            for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++) {
                if (!ShouldConnect (Manager.Peers.ConnectedPeers[i])) {
                    ConnectionManager.CleanupSocket (Manager, Manager.Peers.ConnectedPeers[i]);
                    i--;
                }
            }
        }

        internal void UpdateSeedingDownloadingState ()
        {
            UpdatePartialProgress ();

            //If download is fully complete, set state to 'Seeding' and send an announce to the tracker.
            if (Manager.Complete && state == TorrentState.Downloading) {
                state = TorrentState.Seeding;
                Manager.RaiseTorrentStateChanged (new TorrentStateChangedEventArgs (Manager, TorrentState.Downloading, TorrentState.Seeding));
                _ = Manager.TrackerManager.AnnounceAsync (TorrentEvent.Completed, CancellationToken.None);
            } else if (Manager.PartialProgressSelector.TrueCount > 0) {
                // If some files are marked as DoNotDownload and we have downloaded all downloadable files, mark the torrent as 'seeding'.
                // Otherwise if we have not downloaded all downloadable files, set the state to Downloading.
                if (Manager.Bitfield.CountTrue (Manager.PartialProgressSelector) == Manager.PartialProgressSelector.TrueCount && state == TorrentState.Downloading) {
                    state = TorrentState.Seeding;
                    Manager.RaiseTorrentStateChanged (new TorrentStateChangedEventArgs (Manager, TorrentState.Downloading, TorrentState.Seeding));
                } else if (Manager.Bitfield.CountTrue (Manager.PartialProgressSelector) < Manager.PartialProgressSelector.TrueCount && state == TorrentState.Seeding) {
                    state = TorrentState.Downloading;
                    Manager.RaiseTorrentStateChanged (new TorrentStateChangedEventArgs (Manager, TorrentState.Seeding, TorrentState.Downloading));
                }
            }
        }
    }
}
