//
// TorrentEditor.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2010 Alan McGovern
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


using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public class TorrentEditor : EditableTorrent
    {
        public new bool CanEditSecureMetadata {
            get => base.CanEditSecureMetadata;
            set => base.CanEditSecureMetadata = value;
        }

        public TorrentEditor (BEncodedDictionary metadata)
            : base (metadata)
        {

        }

        public BEncodedDictionary ToDictionary ()
        {
            if (Announces.Count == 0) {
                RemoveCustom ("announce-list");
            } else {
                var list = new BEncodedList ();
                foreach (var rawTier in Announces) {
                    var tier = new BEncodedList ();
                    foreach (string announce in rawTier)
                        tier.Add ((BEncodedString) announce);
                    list.Add (tier);
                }
                SetCustom ("announce-list", list);
            }

            return BEncodedValue.Clone (Metadata);
        }

        public Torrent ToTorrent ()
        {
            return Torrent.Load (ToDictionary ());
        }
    }
}
