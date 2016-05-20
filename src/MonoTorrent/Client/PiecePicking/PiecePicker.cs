using System;
using System.Collections.Generic;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public abstract class PiecePicker
    {
        protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);

        private readonly PiecePicker picker;

        protected PiecePicker(PiecePicker picker)
        {
            this.picker = picker;
        }

        private void CheckOverriden()
        {
            if (picker == null)
                throw new InvalidOperationException("This method must be overridden");
        }

        public virtual void CancelRequest(PeerId peer, int piece, int startOffset, int length)
        {
            CheckOverriden();
            picker.CancelRequest(peer, piece, startOffset, length);
        }

        public virtual void CancelRequests(PeerId peer)
        {
            CheckOverriden();
            picker.CancelRequests(peer);
        }

        public virtual void CancelTimedOutRequests()
        {
            CheckOverriden();
            picker.CancelTimedOutRequests();
        }

        public virtual RequestMessage ContinueExistingRequest(PeerId peer)
        {
            CheckOverriden();
            return picker.ContinueExistingRequest(peer);
        }

        public virtual int CurrentRequestCount()
        {
            CheckOverriden();
            return picker.CurrentRequestCount();
        }

        public virtual List<Piece> ExportActiveRequests()
        {
            CheckOverriden();
            return picker.ExportActiveRequests();
        }

        public virtual void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            CheckOverriden();
            picker.Initialise(bitfield, files, requests);
        }

        public virtual bool IsInteresting(BitField bitfield)
        {
            CheckOverriden();
            return picker.IsInteresting(bitfield);
        }

        public RequestMessage PickPiece(PeerId peer, List<PeerId> otherPeers)
        {
            var bundle = PickPiece(peer, otherPeers, 1);
            return bundle == null ? null : (RequestMessage) bundle.Messages[0];
        }

        public MessageBundle PickPiece(PeerId peer, List<PeerId> otherPeers, int count)
        {
            return PickPiece(peer, peer.BitField, otherPeers, count, 0, peer.BitField.Length);
        }

        public virtual MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count,
            int startIndex, int endIndex)
        {
            CheckOverriden();
            return picker.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);
        }

        public virtual void Reset()
        {
            CheckOverriden();
            picker.Reset();
        }

        public virtual bool ValidatePiece(PeerId peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            CheckOverriden();
            return picker.ValidatePiece(peer, pieceIndex, startOffset, length, out piece);
        }
    }
}