//
// MetadataMode.cs
//
// Authors:
//   Olivier Dufour olivier.duff@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
// Copyright (C) 2009 Olivier Dufour
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

using MonoTorrent.Logging;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;

namespace MonoTorrent.Client.Modes
{
    class FetchHashesMode : Mode
    {
        static readonly Logger logger = Logger.Create (nameof (FetchHashesMode));

        public override bool CanHashCheck => false;
        public override TorrentState State => TorrentState.FetchingHashes;

        public FetchHashesMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, string savePath)
            : this (manager, diskManager, connectionManager, settings, savePath, false)
        {

        }

        public FetchHashesMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, string savePath, bool stopWhenDone)
            : base (manager, diskManager, connectionManager, settings)
        {
            
        }

        bool added = false;
        int test = 0;
        public override void Tick (int counter)
        {
            base.Tick (counter);
            if (test++ == 2) {
                Manager.Peers.AvailablePeers.Add (new Peer ("", new Uri ("ipv4://127.0.0.1:38520")));
            }
        }

        protected override void HandleAllowedFastMessage (PeerId id, AllowedFastMessage message)
        {
            // Disregard these when in metadata mode as we can't request regular pieces anyway
        }

        protected override void HandleHaveAllMessage (PeerId id, HaveAllMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveMessage (PeerId id, HaveMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveNoneMessage (PeerId id, HaveNoneMessage message)
        {
            // Nothing
        }

        protected override void HandleInterestedMessage (PeerId id, InterestedMessage message)
        {
            // Nothing
        }

        protected override void AppendBitfieldMessage (PeerId id, MessageBundle bundle)
        {
            if (id.SupportsFastPeer)
                bundle.Messages.Add (new HaveNoneMessage ());
            // If the fast peer extensions are not supported we must not send a
            // bitfield message because we don't know how many pieces the torrent
            // has. We could probably send an invalid one and force the connection
            // to close.
        }

        protected override void AppendFastPieces (PeerId id, MessageBundle bundle)
        {
            base.AppendFastPieces (id, bundle);
            if (Manager.Torrent.Pieces is HashesV2) {
                bundle.Messages.Add (new HashRequestMessage (Manager.Torrent.Files[0].PiecesRoot.AsMemory (), 3, 0, 4,7));
            }
        }

        protected override void HandleHashesMessage (PeerId id, HashesMessage hashesMessage)
        {
            base.HandleHashesMessage (id, hashesMessage);
        }

        protected override void HandleHashRejectMessage (PeerId id, HashRejectMessage hashRejectMessage)
        {
            base.HandleHashRejectMessage (id, hashRejectMessage);
        }

        protected override void HandleBitfieldMessage (PeerId id, BitfieldMessage message)
        {
            // If we receive a bitfield message we should ignore it. We don't know how many
            // pieces the torrent has so we can't actually safely decode the bitfield.
            if (message != BitfieldMessage.UnknownLength)
                throw new InvalidOperationException ("BitfieldMessages should not be decoded normally while in metadata mode.");
        }

        protected override void SetAmInterestedStatus (PeerId id, bool interesting)
        {
            // Never set a peer as interesting when in metadata mode
            // we don't want to try download any data
        }
    }
}
