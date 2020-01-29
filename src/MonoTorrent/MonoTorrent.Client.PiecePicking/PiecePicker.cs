//
// PiecePicker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Collections.Generic;
using System.Linq;

namespace MonoTorrent.Client.PiecePicking
{
    public abstract class PiecePicker
    {
        protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds (45);

        public PiecePicker BasePicker { get; }

        protected PiecePicker (PiecePicker picker)
        {
            BasePicker = picker;
        }

        void CheckOverriden ()
        {
            if (BasePicker == null)
                throw new InvalidOperationException ("This method must be overridden");
        }

        public virtual void CancelRequest (IPieceRequester peer, int piece, int startOffset, int length)
        {
            CheckOverriden ();
            BasePicker.CancelRequest (peer, piece, startOffset, length);
        }
        public virtual void CancelRequests (IPieceRequester peer)
        {
            CheckOverriden ();
            BasePicker.CancelRequests (peer);
        }
        public virtual void CancelTimedOutRequests ()
        {
            CheckOverriden ();
            BasePicker.CancelTimedOutRequests ();
        }
        public virtual PieceRequest ContinueExistingRequest (IPieceRequester peer)
        {
            CheckOverriden ();
            return BasePicker.ContinueExistingRequest (peer);
        }
        public virtual int CurrentReceivedCount ()
        {
            CheckOverriden ();
            return BasePicker.CurrentReceivedCount ();
        }
        public virtual int CurrentRequestCount ()
        {
            CheckOverriden ();
            return BasePicker.CurrentRequestCount ();
        }
        public virtual List<Piece> ExportActiveRequests ()
        {
            CheckOverriden ();
            return BasePicker.ExportActiveRequests ();
        }
        public virtual void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<Piece> requests)
        {
            CheckOverriden ();
            BasePicker.Initialise (bitfield, torrentData, requests);
        }
        public virtual bool IsInteresting (BitField bitfield)
        {
            CheckOverriden ();
            return BasePicker.IsInteresting (bitfield);
        }
        public PieceRequest PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers)
        {
            IList<PieceRequest> bundle = PickPiece (peer, available, otherPeers, 1);
            return bundle?.Single ();
        }
        public IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count)
        {
            return PickPiece (peer, available, otherPeers, count, 0, available.Length);
        }
        public virtual IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            CheckOverriden ();
            return BasePicker.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);
        }
        public virtual void Reset ()
        {
            CheckOverriden ();
            BasePicker.Reset ();
        }
        public virtual bool ValidatePiece (IPieceRequester peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            CheckOverriden ();
            return BasePicker.ValidatePiece (peer, pieceIndex, startOffset, length, out piece);
        }
    }
}
