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

namespace MonoTorrent.Client.PiecePicking
{
    public class PriorityPicker : PiecePicker
    {
        Predicate<Files> AllSamePriority;

        class Files : IComparable<Files>
        {
            public Priority Priority { get; private set; }
            public ITorrentFileInfo File;

            public Files (ITorrentFileInfo file)
            {
                Priority = file.Priority;
                File = file;
            }

            public int CompareTo (Files other)
                => (int) other.Priority - (int) Priority;

            public bool TryRefreshPriority ()
            {
                if (Priority == File.Priority)
                    return false;

                Priority = File.Priority;
                return true;
            }
        }

        readonly List<Files> files = new List<Files> ();
        readonly List<BitField> prioritised = new List<BitField> ();

        BitField allPrioritisedPieces;
        BitField temp;

        public PriorityPicker (PiecePicker picker)
            : base (picker)
        {

        }

        public override void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<Piece> requests)
        {
            base.Initialise (bitfield, torrentData, requests);
            AllSamePriority = file => file.Priority == files[0].Priority;

            allPrioritisedPieces = new BitField (bitfield.Length);
            temp = new BitField (bitfield.Length);

            files.Clear ();
            for (int i = 0; i < torrentData.Files.Count; i++)
                files.Add (new Files (torrentData.Files[i]));
            BuildSelectors ();
        }

        public override bool IsInteresting (BitField bitfield)
        {
            if (ShouldRebuildSelectors ())
                BuildSelectors ();

            if (files.Count == 1 || files.TrueForAll (AllSamePriority)) {
                if (files[0].Priority == Priority.DoNotDownload)
                    return false;
                return base.IsInteresting (bitfield);
            } else {
                temp.From (allPrioritisedPieces).And (bitfield);
                if (temp.AllFalse)
                    return false;
                return base.IsInteresting (temp);
            }
        }

        public override IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            // Fast Path - the peer has nothing to offer
            if (available.AllFalse)
                return null;

            // Rebuild if any file changed priority
            if (ShouldRebuildSelectors ())
                BuildSelectors ();

            // Fast Path - As 'files' has been sorted highest priority first, all files
            // must be set to DoNotDownload if this is true.
            if (files[0].Priority == Priority.DoNotDownload)
                return null;

            // Fast Path - If it's a single file, or if all the priorities are the same,
            // then we can just pick normally. No prioritisation is needed.
            if (files.Count == 1 || files.TrueForAll (AllSamePriority))
                return base.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);

            // Start with the highest priority and work our way down.
            for (int i = 0; i < prioritised.Count; i++) {
                temp.From (prioritised[i]).And (available);
                if (!temp.AllFalse) {
                    IList<PieceRequest> result = base.PickPiece (peer, temp, otherPeers, count, startIndex, endIndex);
                    if (result != null)
                        return result;
                }
            }

            // None of the pieces from files marked as downloadable were available.
            return null;
        }

        void BuildSelectors ()
        {
            files.Sort ();
            prioritised.Clear ();

            // If it's a single file (or they're all the same priority) then we
            // won't need prioritised bitfields or a bitfield to check the
            // interested status. Set the IsInteresting bitfield to false so
            // it's always in a predictable state and bail out.
            //
            // If all files are set to DoNotDownload we'll bail out early here.
            if (files.Count == 1 || files.TrueForAll (AllSamePriority)) {
                allPrioritisedPieces.SetAll (false);
                return;
            }

            // At least one file is not set to DoNotDownload
            temp.SetAll (false);
            temp.SetTrue (files[0].File.GetSelector ());
            allPrioritisedPieces.From (temp);
            for (int i = 1; i < files.Count && files[i].Priority != Priority.DoNotDownload; i++) {
                allPrioritisedPieces.SetTrue (files[i].File.GetSelector ());

                if (files[i].Priority == files[i - 1].Priority) {
                    temp.SetTrue (files[i].File.GetSelector ());
                } else if (!temp.AllFalse) {
                    prioritised.Add (temp.Clone ());
                    temp.SetAll (false);
                    temp.SetTrue (files[i].File.GetSelector ());
                }
            }

            if (!temp.AllFalse)
                prioritised.Add (temp.Clone ());
        }

        bool ShouldRebuildSelectors ()
        {
            bool needsUpdate = false;
            for (int i = 0; i < files.Count; i++)
                needsUpdate |= files[i].TryRefreshPriority ();
            return needsUpdate;
        }
    }
}
