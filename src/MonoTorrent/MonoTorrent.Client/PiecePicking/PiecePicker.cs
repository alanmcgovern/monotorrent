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
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.FastPeer;

namespace MonoTorrent.Client.PiecePicking
{
    public abstract class PiecePicker
    {
        PiecePicker picker;

        public virtual TimeSpan Timeout
        {
            get
            {
                CheckOverriden();
                return picker.Timeout;
            }
            set
            {
                CheckOverriden();
                picker.Timeout = value;
            }
        }

        protected PiecePicker(PiecePicker picker)
        {
            this.picker = picker;
        }

        void CheckOverriden()
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
        public virtual void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests, BitField unhashedPieces)
        {
            CheckOverriden();
            picker.Initialise(bitfield, files, requests, unhashedPieces);
        }
        public virtual bool IsInteresting(PeerId id)
        {
            CheckOverriden();
            return picker.IsInteresting(id);
        }
        public virtual MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int startIndex, int endIndex, int count)
        {
            CheckOverriden();
            return picker.PickPiece(id, peerBitfield, otherPeers, startIndex, endIndex, count);
        }
        public virtual void Reset()
        {
            CheckOverriden();
            picker.Reset();
        }
        public virtual bool ValidatePiece(PeerId peer, int piece, int startOffset, int length)
        {
            CheckOverriden();
            return picker.ValidatePiece(peer, piece, startOffset, length);
        }
    }
}
