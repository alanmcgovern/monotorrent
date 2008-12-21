//
// PriorityPicker.cs
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
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class PriorityPicker : PiecePicker
    {
        struct Files : IComparable<Files>
        {
            public TorrentFile File;
            public BitField Selector;

            public Files(TorrentFile file, BitField selector)
            {
                File = file;
                Selector = selector;
            }

            public int CompareTo(Files other)
            {
                return other.File.Priority.CompareTo(File.Priority);
            }
        }

        List<Files> files = new List<Files>();
        BitField temp;

        public PriorityPicker(PiecePicker picker)
            : base(picker)
        {

        }

        int FileComparer(TorrentFile left, TorrentFile right)
        {
            return left.Priority.CompareTo(right.Priority);
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            files.Sort();
            temp.SetAll(false);

            if (files.Count == 1)
            {
                if (files[0].File.Priority != Priority.DoNotDownload)
                {
                    temp.Or(peerBitfield);
                    temp.And(files[0].Selector);
                }
                if (temp.AllFalse)
                    return null;
                return base.PickPiece(id, temp, otherPeers, count, startIndex, endIndex);
            }

            temp.Or(files[0].Selector);
            for (int i = 1; i < files.Count; i++)
            {
                if (files[i].File.Priority != files[i - 1].File.Priority)
                {
                    temp.And(peerBitfield);
                    if (!temp.AllFalse)
                    {
                        MessageBundle message = base.PickPiece(id, temp, otherPeers, count, startIndex, endIndex);
                        if (message != null)
                            return message;
                        temp.SetAll(false);
                    }
                }

                if (files[i].File.Priority != Priority.DoNotDownload)
                    temp.Or(files[i].Selector);
            }

            temp.And(peerBitfield);
            if (temp.AllFalse)
                return null;
            return base.PickPiece(id, temp, otherPeers, count, startIndex, endIndex);
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            base.Initialise(bitfield, files, requests);
            temp = new BitField(bitfield.Length);

            this.files.Clear();
            for (int i = 0; i < files.Length; i++)
            {
                BitField b = new BitField(bitfield.Length);
                for (int j = files[i].StartPieceIndex; j <= files[i].EndPieceIndex; j++)
                    b[j] = true;

                this.files.Add(new Files(files[i], b));
            }
        }

        public override bool IsInteresting(BitField bitfield)
        {
            files.Sort();
            temp.SetAll(false);

            // OR all the files together which we want to download
            for (int i = 0; i < files.Count; i++)
                if (files[i].File.Priority != Priority.DoNotDownload)
                    temp.Or(files[i].Selector);
            
            // See which pieces the peer has that we do want to download
            temp.And(bitfield);

            return base.IsInteresting(temp);
        }
    }
}
