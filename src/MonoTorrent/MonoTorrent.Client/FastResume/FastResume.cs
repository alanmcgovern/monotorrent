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
        private static readonly BEncodedString VersionKey = (BEncodedString)"version";
        private static readonly BEncodedString InfoHashKey = (BEncodedString)"infohash";
        private static readonly BEncodedString BitfieldKey = (BEncodedString)"bitfield";
        private static readonly BEncodedString BitfieldLengthKey = (BEncodedString)"bitfield_length";

        private BitField bitfield;
        private InfoHash infoHash;

        public BitField Bitfield
        {
            get { return bitfield; }
        }

        public InfoHash Infohash
        {
            get { return infoHash; }
        }

        public FastResume()
        {
        }

        public FastResume(InfoHash infoHash, BitField bitfield)
        {
            if (infoHash==null)
                throw new ArgumentNullException("infoHash");
            if(bitfield == null)
                throw new ArgumentNullException("bitfield");

            this.infoHash = infoHash;
            this.bitfield = bitfield;
        }

        public FastResume(BEncodedDictionary dict)
        {
            CheckContent(dict, VersionKey, (BEncodedNumber)1);
            CheckContent(dict, InfoHashKey);
            CheckContent(dict, BitfieldKey);
            CheckContent(dict, BitfieldLengthKey);

            infoHash = new InfoHash(((BEncodedString)dict[InfoHashKey]).TextBytes);
            bitfield = new BitField((int)((BEncodedNumber)dict[BitfieldLengthKey]).Number);
            byte[] data = ((BEncodedString)dict[BitfieldKey]).TextBytes;
            bitfield.FromArray(data, 0, data.Length);
        }

        private void CheckContent(BEncodedDictionary dict, BEncodedString key, BEncodedNumber value)
        {
            CheckContent(dict, key);
            if (!dict[key].Equals(value))
                throw new TorrentException(string.Format("Invalid FastResume data. The value of '{0}' was '{1}' instead of '{2}'", key, dict[key], value));
        }

        private void CheckContent(BEncodedDictionary dict, BEncodedString key)
        {
            if (!dict.ContainsKey(key))
                throw new TorrentException(string.Format("Invalid FastResume data. Key '{0}' was not present", key));
        }

        public BEncodedDictionary Encode()
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            dict.Add(VersionKey, (BEncodedNumber)1);
            dict.Add(InfoHashKey, new BEncodedString(infoHash.Hash));
            dict.Add(BitfieldKey, new BEncodedString(bitfield.ToByteArray()));
            dict.Add(BitfieldLengthKey, (BEncodedNumber)bitfield.Length);
            return dict;
        }

        public void Encode(Stream s)
        {
            byte[] data = Encode().Encode();
            s.Write(data, 0, data.Length);
        }
    }
}
