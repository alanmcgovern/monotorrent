//
// FastResume.cs
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


using System;
using System.IO;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    public class FastResume
    {
        // Version 1 stored the Bitfield and Infohash.
        //
        // Version 2 added the UnhashedPieces bitfield.
        //
        static readonly BEncodedNumber FastResumeVersion = 2;

        internal static readonly BEncodedString BitfieldKey = "bitfield";
        internal static readonly BEncodedString BitfieldLengthKey = "bitfield_length";
        internal static readonly BEncodedString InfoHashKey = "infohash";
        internal static readonly BEncodedString UnhashedPiecesKey = "unhashed_pieces";
        internal static readonly BEncodedString VersionKey = "version";

        public BitField Bitfield { get; }

        public InfoHash Infohash { get; }

        public BitField UnhashedPieces { get; }

        [Obsolete ("This constructor should not be used")]
        public FastResume ()
        {
        }

        [Obsolete ("The constructor overload which takes an 'unhashedPieces' parameter should be used instead of this.")]
        public FastResume (InfoHash infoHash, BitField bitfield)
        {
            Infohash = infoHash ?? throw new ArgumentNullException (nameof (infoHash));
            Bitfield = bitfield ?? throw new ArgumentNullException (nameof (bitfield));
            UnhashedPieces = new BitField (Bitfield.Length);
        }

        public FastResume (InfoHash infoHash, BitField bitfield, BitField unhashedPieces)
        {
            Infohash = infoHash ?? throw new ArgumentNullException (nameof (infoHash));
            Bitfield = bitfield?.Clone () ?? throw new ArgumentNullException (nameof (bitfield));
            UnhashedPieces = unhashedPieces?.Clone () ?? throw new ArgumentNullException (nameof (UnhashedPieces));

            for (int i = 0; i < Bitfield.Length; i++) {
                if (bitfield[i] && unhashedPieces[i])
                    throw new ArgumentException ($"The bitfield is set to true at index {i} but that piece is marked as unhashed.");
            }
        }

        public FastResume (BEncodedDictionary dict)
        {
            CheckVersion (dict);
            CheckContent (dict, InfoHashKey);
            CheckContent (dict, BitfieldKey);
            CheckContent (dict, BitfieldLengthKey);

            Infohash = new InfoHash (((BEncodedString) dict[InfoHashKey]).TextBytes);

            Bitfield = new BitField ((int) ((BEncodedNumber) dict[BitfieldLengthKey]).Number);
            byte[] data = ((BEncodedString) dict[BitfieldKey]).TextBytes;
            Bitfield.FromArray (data, 0);

            UnhashedPieces = new BitField (Bitfield.Length);
            // If we're loading up an older version of the FastResume data then we
            if (dict.ContainsKey (UnhashedPiecesKey)) {
                data = ((BEncodedString) dict[UnhashedPiecesKey]).TextBytes;
                UnhashedPieces.FromArray (data, 0);
            }
        }

        static void CheckContent (BEncodedDictionary dict, BEncodedString key)
        {
            if (!dict.ContainsKey (key))
                throw new TorrentException ($"Invalid FastResume data. Key '{key}' was not present");
        }

        static void CheckVersion (BEncodedDictionary dict)
        {
            long? version = (dict[VersionKey] as BEncodedNumber)?.Number;
            if (version.GetValueOrDefault () == 1 || version.GetValueOrDefault () == 2)
                return;

            throw new ArgumentException ($"This FastResume is version {version}, but only version  '1' and '2' are supported");
        }

        public BEncodedDictionary Encode ()
        {
            return new BEncodedDictionary {
                { VersionKey, FastResumeVersion },
                { InfoHashKey, new BEncodedString(Infohash.Hash) },
                { BitfieldKey, new BEncodedString(Bitfield.ToByteArray()) },
                { BitfieldLengthKey, (BEncodedNumber)Bitfield.Length },
                { UnhashedPiecesKey, new BEncodedString (UnhashedPieces.ToByteArray ()) }
            };
        }

        public void Encode (Stream s)
        {
            byte[] data = Encode ().Encode ();
            s.Write (data, 0, data.Length);
        }
    }
}
