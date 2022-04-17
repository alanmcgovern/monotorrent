//
// InitialSeedingMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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

using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;

namespace MonoTorrent.Client.Modes
{
    class InitialSeedingMode : Mode
    {
        readonly ReadOnlyBitField zero;

        new InitialSeedUnchoker Unchoker => (InitialSeedUnchoker) base.Unchoker;

        public override TorrentState State => TorrentState.Seeding;

        public InitialSeedingMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings)
            : base (manager, diskManager, connectionManager, settings, new InitialSeedUnchoker (manager))
        {
            zero = new BitField (manager.Bitfield.Length);
        }

        protected override void AppendBitfieldMessage (PeerId id, MessageBundle bundle)
        {
            if (id.SupportsFastPeer)
                bundle.Add (HaveNoneMessage.Instance, default);
            else
                bundle.Add (new BitfieldMessage (zero), default);
        }

        protected override void HandleHaveMessage (PeerId id, HaveMessage message)
        {
            base.HandleHaveMessage (id, message);
            Unchoker.ReceivedHave (id, message.PieceIndex);
        }

        protected override void HandleRequestMessage (PeerId id, RequestMessage message)
        {
            base.HandleRequestMessage (id, message);
            Unchoker.SentBlock (id, message.PieceIndex);
        }

        protected override void HandleNotInterested (PeerId id, NotInterestedMessage message)
        {
            base.HandleNotInterested (id, message);
            Unchoker.ReceivedNotInterested (id);
        }

        public override void HandlePeerConnected (PeerId id)
        {
            Unchoker.PeerConnected (id);
            base.HandlePeerConnected (id);
        }

        public override void HandlePeerDisconnected (PeerId id)
        {
            base.HandlePeerDisconnected (id);
            Unchoker.PeerDisconnected (id);
        }

        public override void Tick (int counter)
        {
            base.Tick (counter);
            if (Unchoker.Complete) {
                PeerMessage bitfieldMessage = new BitfieldMessage (Manager.Bitfield);
                foreach (PeerId peer in Manager.Peers.ConnectedPeers) {
                    PeerMessage message = peer.SupportsFastPeer && Manager.Complete ? HaveAllMessage.Instance : bitfieldMessage;
                    peer.MessageQueue.Enqueue (message, default);
                }
                Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            }
        }
    }
}
