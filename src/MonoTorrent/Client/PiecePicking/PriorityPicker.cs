using System;
using System.Collections.Generic;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class PriorityPicker : PiecePicker
    {
        private readonly List<Files> files = new List<Files>();
        private Predicate<Files> AllSamePriority;
        private BitField temp;

        public PriorityPicker(PiecePicker picker)
            : base(picker)
        {
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count,
            int startIndex, int endIndex)
        {
            // Fast Path - the peer has nothing to offer
            if (peerBitfield.AllFalse)
                return null;

            if (files.Count == 1)
            {
                if (files[0].File.Priority == Priority.DoNotDownload)
                    return null;
                return base.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);
            }

            files.Sort();

            // Fast Path - all the files have been set to DoNotDownload
            if (files[0].File.Priority == Priority.DoNotDownload)
                return null;

            // Fast Path - If all the files are the same priority, call straight into the base picker
            if (files.TrueForAll(AllSamePriority))
                return base.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);

            temp.From(files[0].Selector);
            for (var i = 1; i < files.Count && files[i].File.Priority != Priority.DoNotDownload; i++)
            {
                if (files[i].File.Priority != files[i - 1].File.Priority)
                {
                    temp.And(peerBitfield);
                    if (!temp.AllFalse)
                    {
                        var message = base.PickPiece(id, temp, otherPeers, count, startIndex, endIndex);
                        if (message != null)
                            return message;
                        temp.SetAll(false);
                    }
                }

                temp.Or(files[i].Selector);
            }

            if (temp.AllFalse || temp.And(peerBitfield).AllFalse)
                return null;
            return base.PickPiece(id, temp, otherPeers, count, startIndex, endIndex);
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            base.Initialise(bitfield, files, requests);
            AllSamePriority = delegate(Files f) { return f.File.Priority == files[0].Priority; };
            temp = new BitField(bitfield.Length);

            this.files.Clear();
            for (var i = 0; i < files.Length; i++)
                this.files.Add(new Files(files[i], files[i].GetSelector(bitfield.Length)));
        }

        public override bool IsInteresting(BitField bitfield)
        {
            files.Sort();
            temp.SetAll(false);

            // OR all the files together which we want to download
            for (var i = 0; i < files.Count; i++)
                if (files[i].File.Priority != Priority.DoNotDownload)
                    temp.Or(files[i].Selector);

            temp.And(bitfield);
            if (temp.AllFalse)
                return false;

            return base.IsInteresting(temp);
        }

        private struct Files : IComparable<Files>
        {
            public readonly TorrentFile File;
            public readonly BitField Selector;

            public Files(TorrentFile file, BitField selector)
            {
                File = file;
                Selector = selector;
            }

            public int CompareTo(Files other)
            {
                return (int) other.File.Priority - (int) File.Priority;
            }
        }
    }
}