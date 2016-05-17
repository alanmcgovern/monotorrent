//
// PeerMessagesTest.cs
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
using System.Text;
using Xunit;
using MonoTorrent.Common;
using System.Net;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages.Libtorrent;

namespace MonoTorrent.Client
{
    
    public class PeerMessagesTest
    {
        TestRig testRig;
        byte[] buffer;
        int offset = 2362;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            buffer = new byte[100000];
            testRig = TestRig.CreateMultiFile();
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            testRig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0xff;
        }

        [TearDown]
        public void GlobalTeardown()
        {
            testRig.Dispose();
        }

        [Fact]
        public void BitFieldEncoding()
        {
            bool[] data = new bool[] { true, false, false, true, false, true, false, true, false, true,
                                       false, true, false, false, false, true, true, true, false, false,
                                       false, true, false, true, false, false, true, false, true, false,
                                       true, true, false, false, true, false, false, true, true, false };
            byte[] encoded = new BitfieldMessage(new BitField(data)).Encode();

            BitfieldMessage m = (BitfieldMessage)PeerMessage.DecodeMessage(encoded, 0, encoded.Length, testRig.Manager);
            Assert.Equal(data.Length, m.BitField.Length, "#1");
            for (int i = 0; i < data.Length; i++)
                Assert.Equal(data[i], m.BitField[i], "#2." + i);
        }

        [Fact]
        public void BitFieldDecoding()
        {
            byte[] buffer = new byte[] { 0x00, 0x00, 0x00, 0x04, 0x05, 0xff, 0x08, 0xAA, 0xE3, 0x00 };
            Console.WriteLine("Pieces: " + testRig.Manager.Torrent.Pieces.Count);
            BitfieldMessage msg = (BitfieldMessage)PeerMessage.DecodeMessage(buffer, 0, 8, this.testRig.Manager);

            for (int i = 0; i < 8; i++)
                Assert.True(msg.BitField[i], i.ToString());

            for (int i = 8; i < 12; i++)
                Assert.False(msg.BitField[i], i.ToString());

            Assert.True(msg.BitField[12], 12.ToString());
            for (int i = 13; i < 15; i++)
                Assert.False(msg.BitField[i], i.ToString());
            EncodeDecode(msg);
        }

        [Ignore("Deliberately broken to work around bugs in azureus")]
        public void BitfieldCorrupt()
        {
            bool[] data = new bool[] { true, false, false, true, false, true, false, true, false, true, false, true, false, false, false, true };
            byte[] encoded = new BitfieldMessage(new BitField(data)).Encode();

            Assert.Throws<MessageException>(() => PeerMessage.DecodeMessage(encoded, 0, encoded.Length, testRig.Manager));
        }



        [Fact]
        public void CancelEncoding()
        {
            int length = new CancelMessage(15, 1024, 16384).Encode(buffer, offset);
            Assert.Equal("00-00-00-0D-08-00-00-00-0F-00-00-04-00-00-00-40-00", BitConverter.ToString(buffer, offset, length));
        }
        [Fact]
        public void CancelDecoding()
        {
            EncodeDecode(new CancelMessage(563, 4737, 88888));
        }



        [Fact]
        public void ChokeEncoding()
        {
            int length = new ChokeMessage().Encode(buffer, offset);
            Assert.Equal("00-00-00-01-00", BitConverter.ToString(buffer, offset, length));
        }
        [Fact]
        public void ChokeDecoding()
        {
            EncodeDecode(new ChokeMessage());
        }



        [Fact]
        public void HandshakeEncoding()
        {
            byte[] infohash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52 };
            int length = new HandshakeMessage(new InfoHash (infohash), "12312312345645645678", VersionInfo.ProtocolStringV100, false, false).Encode(buffer, offset);

            Console.WriteLine(BitConverter.ToString(buffer, offset, length));
            byte[] peerId = Encoding.ASCII.GetBytes("12312312345645645678");
            byte[] protocolVersion = Encoding.ASCII.GetBytes(VersionInfo.ProtocolStringV100);
            Assert.Equal(19, buffer[offset], "1");
            Assert.True(Toolbox.ByteMatch(protocolVersion, 0, buffer, offset + 1, 19), "2");
            Assert.True(Toolbox.ByteMatch(new byte[8], 0, buffer, offset + 20, 8), "3");
            Assert.True(Toolbox.ByteMatch(infohash, 0, buffer, offset + 28, 20), "4");
            Assert.True(Toolbox.ByteMatch(peerId, 0, buffer, offset + 48, 20), "5");
            Assert.Equal(length, 68, "6");

            length = new HandshakeMessage(new InfoHash (infohash), "12312312345645645678", VersionInfo.ProtocolStringV100, true, false).Encode(buffer, offset);
            Assert.Equal(BitConverter.ToString(buffer, offset, length), "13-42-69-74-54-6F-72-72-65-6E-74-20-70-72-6F-74-6F-63-6F-6C-00-00-00-00-00-00-00-04-01-02-03-04-05-06-07-08-09-0A-0B-0C-0D-0E-0F-00-0C-0F-0C-34-31-32-33-31-32-33-31-32-33-34-35-36-34-35-36-34-35-36-37-38", "#7");
        }

        [Fact]
        public void HandshakeDecoding()
        {
            byte[] infohash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52 };
            HandshakeMessage orig = new HandshakeMessage(new InfoHash (infohash), "12312312345645645678", VersionInfo.ProtocolStringV100);
            orig.Encode(buffer, offset);
            HandshakeMessage dec = new HandshakeMessage();
            dec.Decode(buffer, offset, 68);
            Assert.True(orig.Equals(dec));
            Assert.Equal(orig.Encode(), dec.Encode());
        }



        [Fact]
        public void HaveEncoding()
        {
            int length = new HaveMessage(150).Encode(buffer, offset);
            Assert.Equal("00-00-00-05-04-00-00-00-96", BitConverter.ToString(buffer, offset, length));
        }
        [Fact]
        public void HaveDecoding()
        {
            EncodeDecode(new HaveMessage(34622));
        }



        [Fact]
        public void InterestedEncoding()
        {
            int length = new InterestedMessage().Encode(buffer, offset);
            Assert.Equal("00-00-00-01-02", BitConverter.ToString(buffer, offset, length));
        }
        [Fact]
        public void InterestedDecoding()
        {
            EncodeDecode(new InterestedMessage());
        }



        [Fact]
        public void KeepAliveEncoding()
        {
            new KeepAliveMessage().Encode(buffer, offset);
            Assert.True(buffer[offset] == 0
                            && buffer[offset + 1] == 0
                            && buffer[offset + 2] == 0
                            && buffer[offset + 3] == 0);
        }
        [Fact]
        public void KeepAliveDecoding()
        {
            
        }



        [Fact]
        public void NotInterestedEncoding()
        {
            int length = new NotInterestedMessage().Encode(buffer, offset);
            Assert.Equal("00-00-00-01-03", BitConverter.ToString(buffer, offset, length));
        }
        [Fact]
        public void NotInterestedDecoding()
        {
            EncodeDecode(new NotInterestedMessage());
        }



        [Fact]
        public void PieceEncoding()
        {
            PieceMessage message = new PieceMessage(15, 10, Piece.BlockSize);
            message.Data = new byte[Piece.BlockSize];
            message.Encode(buffer, offset);
        }
        [Fact]
        public void PieceDecoding()
        {
            PieceMessage message = new PieceMessage(15, 10, Piece.BlockSize);
            message.Data = new byte[Piece.BlockSize];
            EncodeDecode(message);
        }



        [Fact]
        public void PortEncoding()
        {
            int length = new PortMessage(2500).Encode(buffer, offset);
            Assert.Equal("00-00-00-03-09-09-C4", BitConverter.ToString(buffer, offset, length));
        }
        [Fact]
        public void PortDecoding()
        {
            EncodeDecode(new PortMessage(5452));
        }



        [Fact]
        public void RequestEncoding()
        {
            int length = new RequestMessage(5, 1024, 16384).Encode(buffer, offset);
            Assert.Equal("00-00-00-0D-06-00-00-00-05-00-00-04-00-00-00-40-00", BitConverter.ToString(buffer, offset, length));
        }
        [Fact]
        public void RequestDecoding()
        {
            EncodeDecode(new RequestMessage(123, 789, 4235));
        }



        [Fact]
        public void UnchokeEncoding()
        {
            int length = new UnchokeMessage().Encode(buffer, offset);
            Assert.Equal("00-00-00-01-01", BitConverter.ToString(buffer, offset, length));
        }
        [Fact]
        public void UnchokeDecoding()
        {
            EncodeDecode(new UnchokeMessage());
        }

		[Fact]
		public void PeerExchangeMessageTest ()
		{
			var data = new BEncodedDictionary ().Encode ();
			var message = new PeerExchangeMessage ();
			message.Decode (data, 0, data.Length);
			Assert.NotNull (message.Added, "#1");
			Assert.NotNull (message.AddedDotF, "#1");
			Assert.NotNull (message.Dropped, "#1");
		}

        private void EncodeDecode(Message orig)
        {
            orig.Encode(buffer, offset);
            Message dec = PeerMessage.DecodeMessage(buffer, offset, orig.ByteLength, testRig.Manager);
            Assert.True(orig.Equals(dec), string.Format("orig: {0}, new: {1}", orig, dec));

            Assert.True(Toolbox.ByteMatch(orig.Encode(), PeerMessage.DecodeMessage(orig.Encode(), 0, orig.ByteLength, testRig.Manager).Encode()));
        }
    }
}
