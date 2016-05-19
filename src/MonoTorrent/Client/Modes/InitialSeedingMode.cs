using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class InitialSeedingMode : Mode
    {
        private readonly InitialSeedUnchoker unchoker;
        private readonly BitField zero;

        public InitialSeedingMode(TorrentManager manager)
            : base(manager)
        {
            unchoker = new InitialSeedUnchoker(manager);
            manager.chokeUnchoker = unchoker;
            zero = new BitField(manager.Bitfield.Length);
        }

        public override TorrentState State
        {
            get { return TorrentState.Seeding; }
        }

        protected override void AppendBitfieldMessage(PeerId id, MessageBundle bundle)
        {
            if (id.SupportsFastPeer)
                bundle.Messages.Add(new HaveNoneMessage());
            else
                bundle.Messages.Add(new BitfieldMessage(zero));
        }

        protected override void HandleHaveMessage(PeerId id, HaveMessage message)
        {
            base.HandleHaveMessage(id, message);
            unchoker.ReceivedHave(id, message.PieceIndex);
        }

        protected override void HandleRequestMessage(PeerId id, RequestMessage message)
        {
            base.HandleRequestMessage(id, message);
            unchoker.SentBlock(id, message.PieceIndex);
        }

        protected override void HandleNotInterested(PeerId id, NotInterestedMessage message)
        {
            base.HandleNotInterested(id, message);
            unchoker.ReceivedNotInterested(id);
        }

        public override void HandlePeerConnected(PeerId id, Direction direction)
        {
            base.HandlePeerConnected(id, direction);
            unchoker.PeerConnected(id);
        }

        public override void HandlePeerDisconnected(PeerId id)
        {
            unchoker.PeerDisconnected(id);
            base.HandlePeerDisconnected(id);
        }

        public override void Tick(int counter)
        {
            base.Tick(counter);
            if (unchoker.Complete)
            {
                PeerMessage bitfieldMessage = new BitfieldMessage(Manager.Bitfield);
                PeerMessage haveAllMessage = new HaveAllMessage();
                foreach (var peer in Manager.Peers.ConnectedPeers)
                {
                    var message = peer.SupportsFastPeer && Manager.Complete ? haveAllMessage : bitfieldMessage;
                    peer.Enqueue(message);
                }
                Manager.Mode = new DownloadMode(Manager);
            }
        }
    }
}