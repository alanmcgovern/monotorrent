using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    class InitialSeedingMode : Mode
    {
        BitField zero;
        InitialSeed initialSeed;	//superseed class manager

        
        //return settings.InitialSeedingEnabled
        //    && state == TorrentState.Seeding
        //    && ClientEngine.SupportsInitialSeed;

        //
        //

        //    if (ClientEngine.SupportsInitialSeed)
        //this.initialSeed = (settings.InitialSeedingEnabled ? (new InitialSeed(this)) : null);

            
        public InitialSeedingMode(TorrentManager manager)
            : base(manager)
        {
            initialSeed = new InitialSeed(manager);
            zero = new BitField(manager.Bitfield.Length);
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

            PeerId originPeer = initialSeed.GetOriginPeer(message.PieceIndex);
            if (originPeer != null && originPeer != id)
            {
                initialSeed.OnNotInitialPeerHaveMessage(message.PieceIndex);
                int nextPiece = initialSeed.GetNextPieceForPeer(originPeer);
                if (nextPiece != -1)
                    originPeer.Enqueue(new HaveMessage(nextPiece));
            }
        }
    }
}
