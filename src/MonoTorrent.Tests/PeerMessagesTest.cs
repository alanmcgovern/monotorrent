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
using MonoTorrent.Client.Messages.PeerMessages;

namespace MonoTorrent.Client.Tests
{
    [TestFixture]
    public class PeerMessagesTest
    {
        byte[] buffer = new byte[100000];
        int offset = 2362;

        [SetUp]
        public void Setup()
        {
            buffer = new byte[100000];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0xff;
        }
        [Test]
        [Ignore]
        public void IPeerMessageDecoding()
        {
            PeerMessage msg;
            PeerMessage result;
            TorrentManager manager = null;
            byte[] buffer = new byte[1000];
            /*
                       

                        BitField bf = new BitField(307);
                        for (int i = 0; i < bf.Length; i++)
                            if (i % 5 == 0 || i % 13 == 0)
                                bf[i] = true;
                      */
            //msg = new BitfieldMessage(bf);
            //msg.Encode(buffer, 0);
            //result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            //Assert.AreEqual(msg, result, msg.ToString() + " decoding failed. 1");


            msg = new CancelMessage();
            msg.Encode(buffer, 0);
            result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed. 2");


            msg = new ChokeMessage();
            msg.Encode(buffer, 0);
            result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed. 3");


            //msg = new HandshakeMessage();
            //msg.Encode(buffer, 0);
            //result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            //Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new HaveMessage();
            msg.Encode(buffer, 0);
            result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new InterestedMessage();
            msg.Encode(buffer, 0);
            result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new KeepAliveMessage();
            msg.Encode(buffer, 0);
            Assert.IsTrue(buffer[0] == 0
                          && buffer[1] == 0
                          && buffer[2] == 0
                          && buffer[3] == 0);


            msg = new NotInterestedMessage();
            msg.Encode(buffer, 0);
            result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            //msg = new PieceMessage();
            //msg.Encode(buffer, 0);
            //result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            //Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new PortMessage();
            msg.Encode(buffer, 0);
            result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new RequestMessage();
            msg.Encode(buffer, 0);
            result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new UnchokeMessage();
            msg.Encode(buffer, 0);
            result = PeerMessage.DecodeMessage(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");
        }

        [Test]
        [Ignore]
        public void BitFieldEncoding()
        {/*
            byte[] buffer = new byte[0];
            BitField bf = new BitField(100);
            for (int i = 0; i < 100; i++)
                if (i % 5 == 0)
                    bf[i] = true;

            BitfieldMessage msg = new BitfieldMessage(bf);
            byte[] result = new byte[msg.ByteLength];
            msg.Encode(result, 0);

            Assert.AreEqual(System.Web.HttpUtility.UrlEncode(buffer), System.Web.HttpUtility.UrlEncode(result), "Encoded bitfield incorrectly");
            throw new NotImplementedException();*/
        }
        [Test]
        [Ignore]
        public void BitFieldDecoding()
        {/*
            BitField bf = new BitField(100);
            for (int i = 0; i < 100; i++)
                if (i % 5 == 0)
                    bf[i] = true;

            byte[] bfBytes = new byte[] { 5, 6, 7, 8, 9, 10, 0, 0, 0, 0, 0, 0, 0 };
            bf.FromArray(bfBytes, 0, bfBytes.Length);

            for (int i = 0; i < bf.Length; i++)
                if (i % 5 == 0)
                    Assert.AreEqual(true, bf[i], "Bitfield " + i.ToString() + " is incorrect");
                else
                    Assert.AreEqual(false, bf[i], "Bitfield " + i.ToString() + " is incorrect");

            throw new NotImplementedException();*/
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
            byte[] infohash = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52 };
            int length = new HandshakeMessage(infohash, "12312312345645645678", VersionInfo.ProtocolStringV100, false).Encode(buffer, offset);

            byte[] peerId = Encoding.ASCII.GetBytes("12312312345645645678");
            byte[] protocolVersion = Encoding.ASCII.GetBytes(VersionInfo.ProtocolStringV100);
            Assert.AreEqual(19, buffer[offset], "1");
            Assert.IsTrue(Toolbox.ByteMatch(protocolVersion, 0, buffer, offset + 1, 19), "2");
            Assert.IsTrue(Toolbox.ByteMatch(new byte[8], 0, buffer, offset + 20, 8), "3");
            Assert.IsTrue(Toolbox.ByteMatch(infohash, 0, buffer, offset + 28, 20), "4");
            Assert.IsTrue(Toolbox.ByteMatch(peerId, 0, buffer, offset + 48, 20), "5");
            Assert.AreEqual(length, 68, "6");
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
        [Ignore]
        public void PieceEncoding()
        {
            int length = new PieceMessage(null, 15, 1024, 16384).Encode(buffer, offset);
            throw new NotImplementedException();
        }
        [Test]
        [Ignore]
        public void PieceDecoding()
        {
            EncodeDecode(new PieceMessage(null, 123, 456, 789));
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
            Message dec = PeerMessage.DecodeMessage(buffer, offset + 4, 1000, null); // We need the +4 to skip past the message length bytes
            Assert.IsTrue(orig.Equals(dec), string.Format("orig: {0}, new: {1}", orig, dec));
        }
    }
}
