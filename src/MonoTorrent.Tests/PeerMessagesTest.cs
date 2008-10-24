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
using NUnit.Framework;
using MonoTorrent.Common;
using System.Net;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PeerMessagesTest
    {
        TestRig testRig;
        byte[] buffer = new byte[100000];
        int offset = 2362;

        [SetUp]
        public void Setup()
        {
            buffer = new byte[100000];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0xff;
            testRig = new TestRig("Downloads");
        }

        [TearDown]
        public void GlobalTeardown()
        {
            testRig.Dispose();
        }


        [Test]
        public void BitFieldEncoding()
        {
            bool[] data = new bool[] { true, false, false, true, false, true, false, true, false, true,
                                       false, true, false, false, false, true, true, true, false, false,
                                       false, true, false, true, false, false, true, false, true, false,
                                       true, true, false, false, true, false, false, true, true, false };
            byte[] encoded = new BitfieldMessage(new BitField(data)).Encode();

            BitfieldMessage m = (BitfieldMessage)PeerMessage.DecodeMessage(encoded, 0, encoded.Length, testRig.Manager);
            Assert.AreEqual(data.Length, m.BitField.Length, "#1");
            for (int i = 0; i < data.Length; i++)
                Assert.AreEqual(data[i], m.BitField[i], "#2." + i);
        }

        [Test]
        public void BitFieldDecoding()
        {
            byte[] buffer = new byte[] { 0x00, 0x00, 0x00, 0x04, 0x05, 0xff, 0x08, 0xAA, 0xE3, 0x00 };
            Console.WriteLine("Pieces: " + testRig.Manager.Torrent.Pieces.Count);
            BitfieldMessage msg = (BitfieldMessage)PeerMessage.DecodeMessage(buffer, 0, 8, this.testRig.Manager);

            for (int i = 0; i < 8; i++)
                Assert.IsTrue(msg.BitField[i], i.ToString());

            for (int i = 8; i < 12; i++)
                Assert.IsFalse(msg.BitField[i], i.ToString());

            Assert.IsTrue(msg.BitField[12], 12.ToString());
            for (int i = 13; i < 15; i++)
                Assert.IsFalse(msg.BitField[i], i.ToString());
        }

        [ExpectedException(typeof(MessageException))]
        [Ignore("Deliberately broken to work around bugs in azureus")]
        public void BitfieldCorrupt()
        {
            bool[] data = new bool[] { true, false, false, true, false, true, false, true, false, true, false, true, false, false, false, true };
            byte[] encoded = new BitfieldMessage(new BitField(data)).Encode();

            BitfieldMessage m = (BitfieldMessage)PeerMessage.DecodeMessage(encoded, 0, encoded.Length, testRig.Manager);
        }



        [Test]
        public void CancelEncoding()
        {
            int length = new CancelMessage(15, 1024, 16384).Encode(buffer, offset);
            Assert.AreEqual("00-00-00-0D-08-00-00-00-0F-00-00-04-00-00-00-40-00", BitConverter.ToString(buffer, offset, length));
        }
        [Test]
        public void CancelDecoding()
        {
            EncodeDecode(new CancelMessage(563, 4737, 88888));
        }



        [Test]
        public void ChokeEncoding()
        {
            int length = new ChokeMessage().Encode(buffer, offset);
            Assert.AreEqual("00-00-00-01-00", BitConverter.ToString(buffer, offset, length));
        }
        [Test]
        public void ChokeDecoding()
        {
            EncodeDecode(new ChokeMessage());
        }



        [Test]
        public void HandshakeEncoding()
        {
            byte[] infohash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52 };
            int length = new HandshakeMessage(infohash, "12312312345645645678", VersionInfo.ProtocolStringV100, false, false).Encode(buffer, offset);

            Console.WriteLine(BitConverter.ToString(buffer, offset, length));
            byte[] peerId = Encoding.ASCII.GetBytes("12312312345645645678");
            byte[] protocolVersion = Encoding.ASCII.GetBytes(VersionInfo.ProtocolStringV100);
            Assert.AreEqual(19, buffer[offset], "1");
            Assert.IsTrue(Toolbox.ByteMatch(protocolVersion, 0, buffer, offset + 1, 19), "2");
            Assert.IsTrue(Toolbox.ByteMatch(new byte[8], 0, buffer, offset + 20, 8), "3");
            Assert.IsTrue(Toolbox.ByteMatch(infohash, 0, buffer, offset + 28, 20), "4");
            Assert.IsTrue(Toolbox.ByteMatch(peerId, 0, buffer, offset + 48, 20), "5");
            Assert.AreEqual(length, 68, "6");

            length = new HandshakeMessage(infohash, "12312312345645645678", VersionInfo.ProtocolStringV100, true, false).Encode(buffer, offset);
            Assert.AreEqual(BitConverter.ToString(buffer, offset, length), "13-42-69-74-54-6F-72-72-65-6E-74-20-70-72-6F-74-6F-63-6F-6C-00-00-00-00-00-00-00-04-01-02-03-04-05-06-07-08-09-0A-0B-0C-0D-0E-0F-00-0C-0F-0C-34-31-32-33-31-32-33-31-32-33-34-35-36-34-35-36-34-35-36-37-38", "#7");
        }

        [Test]
        public void HandshakeDecoding()
        {
            byte[] infohash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52 };
            HandshakeMessage orig = new HandshakeMessage(infohash, "12312312345645645678", VersionInfo.ProtocolStringV100);
            orig.Encode(buffer, offset);
            HandshakeMessage dec = new HandshakeMessage();
            dec.Decode(buffer, offset, 68);
            Assert.IsTrue(orig.Equals(dec));
        }



        [Test]
        public void HaveEncoding()
        {
            int length = new HaveMessage(150).Encode(buffer, offset);
            Assert.AreEqual("00-00-00-05-04-00-00-00-96", BitConverter.ToString(buffer, offset, length));
        }
        [Test]
        public void HaveDecoding()
        {
            EncodeDecode(new HaveMessage(34622));
        }



        [Test]
        public void InterestedEncoding()
        {
            int length = new InterestedMessage().Encode(buffer, offset);
            Assert.AreEqual("00-00-00-01-02", BitConverter.ToString(buffer, offset, length));
        }
        [Test]
        public void InterestedDecoding()
        {
            EncodeDecode(new InterestedMessage());
        }



        [Test]
        public void KeepAliveEncoding()
        {
            new KeepAliveMessage().Encode(buffer, offset);
            Assert.IsTrue(buffer[offset] == 0
                            && buffer[offset + 1] == 0
                            && buffer[offset + 2] == 0
                            && buffer[offset + 3] == 0);
        }
        [Test]
        public void KeepAliveDecoding()
        {
            // Keep alives aren't "decoded" as they have 0 message id and 0 body length
        }



        [Test]
        public void NotInterestedEncoding()
        {
            int length = new NotInterestedMessage().Encode(buffer, offset);
            Assert.AreEqual("00-00-00-01-03", BitConverter.ToString(buffer, offset, length));
        }
        [Test]
        public void NotInterestedDecoding()
        {
            EncodeDecode(new NotInterestedMessage());
        }



        [Test]
        public void PieceEncoding()
        {
            int length = new PieceMessage(testRig.Manager, 15, 1024, 16384).Encode(buffer, offset);
        }
        [Test]
        public void PieceDecoding()
        {
            EncodeDecode(new PieceMessage(testRig.Manager, 123, 456, 789));
        }



        [Test]
        public void PortEncoding()
        {
            int length = new PortMessage(2500).Encode(buffer, offset);
            Assert.AreEqual("00-00-00-03-09-09-C4", BitConverter.ToString(buffer, offset, length));
        }
        [Test]
        public void PortDecoding()
        {
            EncodeDecode(new PortMessage(5452));
        }



        [Test]
        public void RequestEncoding()
        {
            int length = new RequestMessage(5, 1024, 16384).Encode(buffer, offset);
            Assert.AreEqual("00-00-00-0D-06-00-00-00-05-00-00-04-00-00-00-40-00", BitConverter.ToString(buffer, offset, length));
        }
        [Test]
        public void RequestDecoding()
        {
            EncodeDecode(new RequestMessage(123, 789, 4235));
        }



        [Test]
        public void UnchokeEncoding()
        {
            int length = new UnchokeMessage().Encode(buffer, offset);
            Assert.AreEqual("00-00-00-01-01", BitConverter.ToString(buffer, offset, length));
        }
        [Test]
        public void UnchokeDecoding()
        {
            EncodeDecode(new UnchokeMessage());
        }

        private void EncodeDecode(Message orig)
        {
            orig.Encode(buffer, offset);
            Message dec = PeerMessage.DecodeMessage(buffer, offset, orig.ByteLength, null);
            Assert.IsTrue(orig.Equals(dec), string.Format("orig: {0}, new: {1}", orig, dec));
        }
    }
}
