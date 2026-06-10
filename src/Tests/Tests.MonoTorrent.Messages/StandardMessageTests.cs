//
// StandardMessageTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Text;

using MonoTorrent.Messages;

using NUnit.Framework;

namespace MonoTorrent.Messages.Peer
{
    [TestFixture]
    public class StandardMessageTests
    {
        byte[] buffer;
        int offset;
        ITorrentManagerInfo torrentData;

        [SetUp]
        public void Setup ()
        {
            buffer = new byte[100000];
            offset = 2362;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0xff;

            torrentData = TestTorrentManagerInfo.Create (
                pieceLength: 16 * Constants.BlockSize,
                size: 40 * 16 * Constants.BlockSize
            );
        }

        [Test]
        public void BitFieldEncoding ()
        {
            bool[] data = { true, false, false, true, false, true, false, true, false, true,
                                       false, true, false, false, false, true, true, true, false, false,
                                       false, true, false, true, false, false, true, false, true, false,
                                       true, true, false, false, true, false, false, true, true, false };

            Assert.AreEqual (data.Length, (int) Math.Ceiling ((double) torrentData.TorrentInfo.Size / torrentData.TorrentInfo.PieceLength), "#0");
            (ReadOnlyMemory<byte> encoded, var releaser) = MessageEncoder.WriteBitfield(new ReadOnlyBitField (data));

            BitfieldMessage m = new BitfieldMessage(encoded);
            var bitfield = new BitField (m.BitField, torrentData.TorrentInfo.PieceCount ());
            Assert.AreEqual (5, m.BitField.Length, "#0");
            Assert.AreEqual (data.Length, bitfield.Length, "#1");
            for (int i = 0; i < data.Length; i++)
                Assert.AreEqual (data[i], bitfield[i], "#2." + i);
        }

        [Test]
        public void BitFieldDecoding ()
        {
            byte[] buf = { 0x00, 0x00, 0x00, 0x04, 0x05, 0xff, 0x08, 0xAA, 0xE3, 0x00 };
            BitfieldMessage msg = new BitfieldMessage (buf);

            var bitField = new BitField (msg.BitField, torrentData.TorrentInfo.PieceCount ());
            for (int i = 0; i < 8; i++)
                Assert.IsTrue (bitField[i], i.ToString ());

            for (int i = 8; i < 12; i++)
                Assert.IsFalse (bitField[i], i.ToString ());

            Assert.IsTrue (bitField[12], 12.ToString ());
            for (int i = 13; i < 15; i++)
                Assert.IsFalse (bitField[i], i.ToString ());
        }

        [Test]
        public void CancelEncoding ()
        {
            int length = MessageEncoder.WriteCancel (buffer.AsSpan (offset), 15, 1024, 16384);
            Assert.AreEqual ("00-00-00-0D-08-00-00-00-0F-00-00-04-00-00-00-40-00", BitConverter.ToString (buffer, offset, length));
        }

        [Test]
        public void ChokeEncoding ()
        {
            int length = MessageEncoder.WriteChoke(buffer.AsSpan(offset));
            Assert.AreEqual ("00-00-00-01-00", BitConverter.ToString (buffer, offset, length));
        }

        [Test]
        public void HandshakeEncoding ()
        {
            byte[] infohash = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52 };
            int length = MessageEncoder.WriteHandshake (buffer.AsSpan (offset), infohash.AsSpan (), "12312312345645645678"u8, false, false, false);

            byte[] peerId = Encoding.ASCII.GetBytes ("12312312345645645678");
            byte[] protocolVersion = Encoding.ASCII.GetBytes (Constants.ProtocolStringV100);
            Assert.AreEqual (19, buffer[offset], "1");
            Assert.IsTrue (protocolVersion.AsSpan ().SequenceEqual (buffer.AsSpan (offset + 1, 19)), "2");
            Assert.IsTrue (new byte[8].AsSpan ().SequenceEqual (buffer.AsSpan (offset + 20, 8)), "3");
            Assert.IsTrue (infohash.AsSpan ().SequenceEqual (buffer.AsSpan (offset + 28, 20)), "4");
            Assert.IsTrue (peerId.AsSpan ().SequenceEqual (buffer.AsSpan (offset + 48, 20)), "5");
            Assert.AreEqual (length, HandshakeMessage.HandshakeLength, "6");

            length = MessageEncoder.WriteHandshake (buffer.AsSpan (offset), infohash, "12312312345645645678"u8, true, false, false);
            Assert.AreEqual (BitConverter.ToString (buffer, offset, length), "13-42-69-74-54-6F-72-72-65-6E-74-20-70-72-6F-74-6F-63-6F-6C-00-00-00-00-00-00-00-04-01-02-03-04-05-06-07-08-09-0A-0B-0C-0D-0E-0F-00-0C-0F-0C-34-31-32-33-31-32-33-31-32-33-34-35-36-34-35-36-34-35-36-37-38", "#7");
        }

        [Test]
        public void HaveEncoding ()
        {
            int length = MessageEncoder.WriteHave (buffer.AsSpan (offset), 150);
            Assert.AreEqual ("00-00-00-05-04-00-00-00-96", BitConverter.ToString (buffer, offset, length));
        }
        
        [Test]
        public void InterestedEncoding ()
        {
            int length = MessageEncoder.WriteInterested (buffer.AsSpan (offset));
            Assert.AreEqual ("00-00-00-01-02", BitConverter.ToString (buffer, offset, length));
        }

        [Test]
        public void KeepAliveEncoding ()
        {
            MessageEncoder.WriteKeepAlive (buffer.AsSpan (offset));
            Assert.IsTrue (buffer[offset] == 0
                            && buffer[offset + 1] == 0
                            && buffer[offset + 2] == 0
                            && buffer[offset + 3] == 0);
        }

        [Test]
        public void NotInterestedEncoding ()
        {
            int length = MessageEncoder.WriteNotInterested(buffer.AsSpan (offset));
            Assert.AreEqual ("00-00-00-01-03", BitConverter.ToString (buffer, offset, length));
        }
       
        [Test]
        public void PieceDecoding ()
        {
            Span<byte> data = new byte[Constants.BlockSize];
            data.Fill (byte.MaxValue);

            (var buffer, var releaser) = MessageEncoder.WriteSparsePiece (15, 10, data.Length);
            MessageEncoder.AppendPieceData (buffer, data);

            var decoded = new PieceMessage (buffer);
            Assert.AreEqual (15, decoded.PieceIndex);
            Assert.AreEqual (10, decoded.StartOffset);
            Assert.AreEqual (data.Length, decoded.RequestLength);
            Assert.AreEqual (255, decoded.Data[0]);
            Assert.AreEqual (255, decoded.Data[data.Length - 1]);
            Assert.AreEqual (data.Length, decoded.Data.Length);
        }



        [Test]
        public void PortEncoding ()
        {
            int length = MessageEncoder.WritePort (buffer.AsSpan (offset), 2500);
            Assert.AreEqual ("00-00-00-03-09-09-C4", BitConverter.ToString (buffer, offset, length));
        }

        [Test]
        public void RequestEncoding ()
        {
            int length = MessageEncoder.WriteRequest (buffer.AsSpan (offset), 5, 1024, 16384);
            Assert.AreEqual ("00-00-00-0D-06-00-00-00-05-00-00-04-00-00-00-40-00", BitConverter.ToString (buffer, offset, length));
        }


        [Test]
        public void UnchokeEncoding ()
        {
            int length = MessageEncoder.WriteUnchoke(buffer.AsSpan (offset));
            Assert.AreEqual ("00-00-00-01-01", BitConverter.ToString (buffer, offset, length));
        }
    }
}
