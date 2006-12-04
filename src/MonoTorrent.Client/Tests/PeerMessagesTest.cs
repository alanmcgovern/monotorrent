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
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;
using System.Net;

namespace MonoTorrent.Client.Tests
{
    [TestFixture]
    public class PeerMessagesTest
    {
        [Test]
        public void IPeerMessageDecoding()
        {
            IPeerMessageInternal msg;
            IPeerMessageInternal result;
            TorrentManager manager = null;

            byte[] buffer = new byte[1000];

            BitField bf = new BitField(307);
            for (int i = 0; i < bf.Length; i++)
                if (i % 5 == 0 || i % 13 == 0)
                    bf[i] = true;

#warning Test BitField Messages too
            //msg = new BitfieldMessage(bf);
            //msg.Encode(buffer, 0);
            //result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            //Assert.AreEqual(msg, result, msg.ToString() + " decoding failed. 1");


            msg = new CancelMessage();
            msg.Encode(buffer, 0);
            result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed. 2");


            msg = new ChokeMessage();
            msg.Encode(buffer, 0);
            result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed. 3");


            #warning Update this test when HandshakeMessage is an IPeerMessage
            //msg = new HandshakeMessage();
            //msg.Encode(buffer, 0);
            //result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            //Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new HaveMessage();
            msg.Encode(buffer, 0);
            result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new InterestedMessage();
            msg.Encode(buffer, 0);
            result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new KeepAliveMessage();
            msg.Encode(buffer, 0);
            Assert.IsTrue(buffer[0] == 0
                          && buffer[1] == 0
                          && buffer[2] == 0
                          && buffer[3] == 0);


            msg = new NotInterestedMessage();
            msg.Encode(buffer, 0);
            result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


#warning Update this test
            //msg = new PieceMessage();
            //msg.Encode(buffer, 0);
            //result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            //Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new PortMessage();
            msg.Encode(buffer, 0);
            result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new RequestMessage();
            msg.Encode(buffer, 0);
            result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");


            msg = new UnchokeMessage();
            msg.Encode(buffer, 0);
            result = PeerwireEncoder.Decode(buffer, 4, msg.ByteLength, manager);
            Assert.AreEqual(msg, result, msg.ToString() + " decoding failed");
        }

        [Test]
        public void BitFieldEncoding()
        {
            byte[] buffer = new byte[0];
            BitField bf = new BitField(100);
            for (int i = 0; i < 100; i++)
                if (i % 5 == 0)
                    bf[i] = true;

            BitfieldMessage msg = new BitfieldMessage(bf);
            byte[] result = new byte[msg.ByteLength];
            msg.Encode(result, 0);
            
            Assert.AreEqual(System.Web.HttpUtility.UrlEncode(buffer), System.Web.HttpUtility.UrlEncode(result), "Encoded bitfield incorrectly");
            throw new NotImplementedException();
        }
        [Test]
        public void BitFieldDecoding()
        {
            BitField bf = new BitField(100);
            for (int i = 0; i < 100; i++)
                if (i % 5 == 0)
                    bf[i] = true;

            byte[] bfBytes = new byte[] { 5, 6, 7, 8, 9, 10 };
            bf.FromArray(bfBytes, 0, bfBytes.Length);

            for (int i = 0; i < bf.Length; i++)
                if (i % 5 == 0)
                    Assert.AreEqual(true, bf[i], "Bitfield " + i.ToString() + " is incorrect");
                else
                    Assert.AreEqual(false, bf[i], "Bitfield " + i.ToString() + " is incorrect");

            throw new NotImplementedException();
        }



        [Test]
        public void CancelEncoding()
        {
            IPeerMessageInternal msg = new CancelMessage(15, 1024, 16384);
            byte[] buffer = new byte[msg.ByteLength];
            msg.Encode(buffer, 0);
            Console.WriteLine(BitConverter.ToString(buffer));
            Assert.AreEqual("00-00-00-0D-08-00-00-00-0F-00-00-04-00-00-00-40-00", BitConverter.ToString(buffer));
        }
        [Test]
        public void CancelDecoding()
        {
            byte[] buffer = new byte[17];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(13)), 0, buffer, 0, 4);
            buffer[4] = (byte)8;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(15)), 0, buffer, 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1024)), 0, buffer, 9, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(16384)), 0, buffer, 13, 4);
            Assert.IsTrue(PeerwireEncoder.Decode(buffer, 4, 13, null) is CancelMessage);
        }



        [Test]
        public void ChokeEncoding()
        {
            IPeerMessageInternal msg = new ChokeMessage();
            byte[] buffer = new byte[msg.ByteLength];
            msg.Encode(buffer, 0);
            Assert.AreEqual("00-00-00-01-00", BitConverter.ToString(buffer));
        }
        [Test]
        public void ChokeDecoding()
        {
            byte[] buffer = new byte[5];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1)), 0, buffer, 0, 4);
            buffer[4] = (byte)0;

            Assert.IsTrue(PeerwireEncoder.Decode(buffer, 4, 5, null) is ChokeMessage);
        }



        [Test]
        public void HandshakeEncoding()
        {
            throw new NotImplementedException();
        }
        [Test]
        public void HandshakeDecoding()
        {
            throw new NotImplementedException();
        }



        [Test]
        public void HaveEncoding()
        {
            IPeerMessageInternal msg = new HaveMessage(150);
            byte[] buffer = new byte[msg.ByteLength];
            msg.Encode(buffer, 0);
            Assert.AreEqual("00-00-00-05-04-00-00-00-96", BitConverter.ToString(buffer));
        }
        [Test]
        public void HaveDecoding()
        {
            byte[] buffer = new byte[9];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(5)), 0, buffer, 0, 4);
            buffer[4] = (byte)4;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(150)), 0, buffer, 5, 4);
            
            Assert.IsTrue(PeerwireEncoder.Decode(buffer, 4, 5, null) is HaveMessage);
        }



        [Test]
        public void InterestedEncoding()
        {
            IPeerMessageInternal msg = new InterestedMessage();
            byte[] buffer = new byte[msg.ByteLength];
            msg.Encode(buffer, 0);
            Assert.AreEqual("00-00-00-01-02", BitConverter.ToString(buffer));
        }
        [Test]
        public void InterestedDecoding()
        {
            byte[] buffer = new byte[5];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1)), 0, buffer, 0, 4);
            buffer[4] = (byte)2;

            Assert.IsTrue(PeerwireEncoder.Decode(buffer, 4, 1, null) is InterestedMessage);
        }



        [Test]
        public void KeepAliveEncoding()
        {
            byte[] buffer = new byte[4];
            IPeerMessageInternal msg = new KeepAliveMessage();
            msg.Encode(buffer, 0);
            Assert.IsTrue(buffer[0] == 0
                            && buffer[1] == 0
                            && buffer[2] == 0
                            && buffer[3] == 0);
        }
        [Test]
        public void KeepAliveDecoding()
        {
            // Keep alives aren't "decoded" as they have 0 message id and 0 body length
        }



        [Test]
        public void NotInterestedEncoding()
        {
            IPeerMessageInternal msg = new NotInterestedMessage();
            byte[] buffer = new byte[msg.ByteLength];
            msg.Encode(buffer, 0);
            Assert.AreEqual("00-00-00-01-03", BitConverter.ToString(buffer));
        }
        [Test]
        public void NotInterestedDecoding()
        {
            byte[] buffer = new byte[5];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1)), 0, buffer, 0, 4);
            buffer[4] = (byte)3;

            Assert.IsTrue(PeerwireEncoder.Decode(buffer, 4, 1, null) is NotInterestedMessage);
        }



        [Test]
        public void PieceEncoding()
        {
            PieceMessage msg = new PieceMessage(null, 15, 1024, 16384);
            byte[] buffer = new byte[msg.ByteLength];
            msg.Encode(buffer, 0);
        }
        [Test]
        public void PieceDecoding()
        {
            //<len=0009+X><id=7><index><begin><block>
            byte[] buffer = new byte[16384 + 13];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(buffer.Length)), 0, buffer, 0,4);
            buffer[4] = (byte)7;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(15)), 0, buffer, 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1024)), 0, buffer, 9, 4);

            Assert.IsTrue(PeerwireEncoder.Decode(buffer, 4, 3, null) is PieceMessage);
        }



        [Test]
        public void PortEncoding()
        {
            IPeerMessageInternal msg = new PortMessage(2500);
            byte[] buffer = new byte[msg.ByteLength];
            msg.Encode(buffer, 0);
            Assert.AreEqual("00-00-00-03-09-00-00", BitConverter.ToString(buffer));
        }
        [Test]
        public void PortDecoding()
        {
            byte[] buffer = new byte[7];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(3)), 0, buffer, 0,4);
            buffer[4] = (byte)9;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(2500)),0,buffer, 5, 2);

            Assert.IsTrue(PeerwireEncoder.Decode(buffer, 4, 3, null) is PortMessage);
        }



        [Test]
        public void RequestEncoding()
        {
            byte[] buffer = new byte[17];
            RequestMessage msg = new RequestMessage(5, 1024, 16384);
            msg.Encode(buffer, 0);

            Assert.AreEqual("00-00-00-0D-06-00-00-00-05-00-00-04-00-00-00-40-00", BitConverter.ToString(buffer));
        }
        [Test]
        public void RequestDecoding()
        {
            byte[] buffer = new byte[17];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(13)), 0, buffer, 0, 4);
            buffer[4] = 6;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(5)), 0, buffer, 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1024)), 0, buffer, 9, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(16384)), 0, buffer, 13, 4);

            Assert.IsTrue(PeerwireEncoder.Decode(buffer, 4, 13, null) is RequestMessage);
        }



        [Test]
        public void UnchokeEncoding()
        {
            byte[] actual = new byte[5];
            UnchokeMessage msg = new UnchokeMessage();
            msg.Encode(actual, 0);
            Assert.AreEqual("00-00-00-01-01", BitConverter.ToString(actual));
        }
        [Test]
        public void UnchokeDecoding()
        {
            byte[] buffer = new byte[1000];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1)), 0, buffer, 0, 4);
            buffer[4] = (byte)1;

            IPeerMessageInternal msg = PeerwireEncoder.Decode(buffer, 4, 1, null);
            Assert.IsTrue(msg is UnchokeMessage);
        }
    }
}
