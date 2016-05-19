using System;
using System.Text;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Client
{
    public class PeerMessagesTest : IDisposable
    {
        public PeerMessagesTest()
        {
            buffer = new byte[100000];
            testRig = TestRig.CreateMultiFile();

            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = 0xff;
        }

        public void Dispose()
        {
            testRig.Dispose();
        }

        private readonly TestRig testRig;
        private readonly byte[] buffer;
        private readonly int offset = 2362;

        //Deliberately broken to work around bugs in azureus
        public void BitfieldCorrupt()
        {
            var data = new[]
            {true, false, false, true, false, true, false, true, false, true, false, true, false, false, false, true};
            var encoded = new BitfieldMessage(new BitField(data)).Encode();

            Assert.Throws<MessageException>(() => PeerMessage.DecodeMessage(encoded, 0, encoded.Length, testRig.Manager));
        }

        private void EncodeDecode(Message orig)
        {
            orig.Encode(buffer, offset);
            Message dec = PeerMessage.DecodeMessage(buffer, offset, orig.ByteLength, testRig.Manager);
            Assert.True(orig.Equals(dec), string.Format("orig: {0}, new: {1}", orig, dec));

            Assert.True(Toolbox.ByteMatch(orig.Encode(),
                PeerMessage.DecodeMessage(orig.Encode(), 0, orig.ByteLength, testRig.Manager).Encode()));
        }

        [Fact]
        public void BitFieldDecoding()
        {
            var buffer = new byte[] {0x00, 0x00, 0x00, 0x04, 0x05, 0xff, 0x08, 0xAA, 0xE3, 0x00};
            Console.WriteLine("Pieces: " + testRig.Manager.Torrent.Pieces.Count);
            var msg = (BitfieldMessage) PeerMessage.DecodeMessage(buffer, 0, 8, testRig.Manager);

            for (var i = 0; i < 8; i++)
                Assert.True(msg.BitField[i], i.ToString());

            for (var i = 8; i < 12; i++)
                Assert.False(msg.BitField[i], i.ToString());

            Assert.True(msg.BitField[12], 12.ToString());
            for (var i = 13; i < 15; i++)
                Assert.False(msg.BitField[i], i.ToString());
            EncodeDecode(msg);
        }

        [Fact]
        public void BitFieldEncoding()
        {
            var data = new[]
            {
                true, false, false, true, false, true, false, true, false, true,
                false, true, false, false, false, true, true, true, false, false,
                false, true, false, true, false, false, true, false, true, false,
                true, true, false, false, true, false, false, true, true, false
            };
            var encoded = new BitfieldMessage(new BitField(data)).Encode();

            var m = (BitfieldMessage) PeerMessage.DecodeMessage(encoded, 0, encoded.Length, testRig.Manager);
            Assert.Equal(data.Length, m.BitField.Length);
            for (var i = 0; i < data.Length; i++)
                Assert.Equal(data[i], m.BitField[i]);
        }

        [Fact]
        public void CancelDecoding()
        {
            EncodeDecode(new CancelMessage(563, 4737, 88888));
        }


        [Fact]
        public void CancelEncoding()
        {
            var length = new CancelMessage(15, 1024, 16384).Encode(buffer, offset);
            Assert.Equal("00-00-00-0D-08-00-00-00-0F-00-00-04-00-00-00-40-00",
                BitConverter.ToString(buffer, offset, length));
        }

        [Fact]
        public void ChokeDecoding()
        {
            EncodeDecode(new ChokeMessage());
        }


        [Fact]
        public void ChokeEncoding()
        {
            var length = new ChokeMessage().Encode(buffer, offset);
            Assert.Equal("00-00-00-01-00", BitConverter.ToString(buffer, offset, length));
        }

        [Fact]
        public void HandshakeDecoding()
        {
            var infohash = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52};
            var orig = new HandshakeMessage(new InfoHash(infohash), "12312312345645645678",
                VersionInfo.ProtocolStringV100);
            orig.Encode(buffer, offset);
            var dec = new HandshakeMessage();
            dec.Decode(buffer, offset, 68);
            Assert.True(orig.Equals(dec));
            Assert.Equal(orig.Encode(), dec.Encode());
        }


        [Fact]
        public void HandshakeEncoding()
        {
            var infohash = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52};
            var length =
                new HandshakeMessage(new InfoHash(infohash), "12312312345645645678", VersionInfo.ProtocolStringV100,
                    false, false).Encode(buffer, offset);

            Console.WriteLine(BitConverter.ToString(buffer, offset, length));
            var peerId = Encoding.ASCII.GetBytes("12312312345645645678");
            var protocolVersion = Encoding.ASCII.GetBytes(VersionInfo.ProtocolStringV100);
            Assert.Equal(19, buffer[offset]);
            Assert.True(Toolbox.ByteMatch(protocolVersion, 0, buffer, offset + 1, 19), "2");
            Assert.True(Toolbox.ByteMatch(new byte[8], 0, buffer, offset + 20, 8), "3");
            Assert.True(Toolbox.ByteMatch(infohash, 0, buffer, offset + 28, 20), "4");
            Assert.True(Toolbox.ByteMatch(peerId, 0, buffer, offset + 48, 20), "5");
            Assert.Equal(length, 68);

            length =
                new HandshakeMessage(new InfoHash(infohash), "12312312345645645678", VersionInfo.ProtocolStringV100,
                    true, false).Encode(buffer, offset);
            Assert.Equal(BitConverter.ToString(buffer, offset, length),
                "13-42-69-74-54-6F-72-72-65-6E-74-20-70-72-6F-74-6F-63-6F-6C-00-00-00-00-00-00-00-04-01-02-03-04-05-06-07-08-09-0A-0B-0C-0D-0E-0F-00-0C-0F-0C-34-31-32-33-31-32-33-31-32-33-34-35-36-34-35-36-34-35-36-37-38");
        }

        [Fact]
        public void HaveDecoding()
        {
            EncodeDecode(new HaveMessage(34622));
        }


        [Fact]
        public void HaveEncoding()
        {
            var length = new HaveMessage(150).Encode(buffer, offset);
            Assert.Equal("00-00-00-05-04-00-00-00-96", BitConverter.ToString(buffer, offset, length));
        }

        [Fact]
        public void InterestedDecoding()
        {
            EncodeDecode(new InterestedMessage());
        }


        [Fact]
        public void InterestedEncoding()
        {
            var length = new InterestedMessage().Encode(buffer, offset);
            Assert.Equal("00-00-00-01-02", BitConverter.ToString(buffer, offset, length));
        }

        [Fact]
        public void KeepAliveDecoding()
        {
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
        public void NotInterestedDecoding()
        {
            EncodeDecode(new NotInterestedMessage());
        }


        [Fact]
        public void NotInterestedEncoding()
        {
            var length = new NotInterestedMessage().Encode(buffer, offset);
            Assert.Equal("00-00-00-01-03", BitConverter.ToString(buffer, offset, length));
        }

        [Fact]
        public void PeerExchangeMessageTest()
        {
            var data = new BEncodedDictionary().Encode();
            var message = new PeerExchangeMessage();
            message.Decode(data, 0, data.Length);
            Assert.NotNull(message.Added);
            Assert.NotNull(message.AddedDotF);
            Assert.NotNull(message.Dropped);
        }

        [Fact]
        public void PieceDecoding()
        {
            var message = new PieceMessage(15, 10, Piece.BlockSize);
            message.Data = new byte[Piece.BlockSize];
            EncodeDecode(message);
        }


        [Fact]
        public void PieceEncoding()
        {
            var message = new PieceMessage(15, 10, Piece.BlockSize);
            message.Data = new byte[Piece.BlockSize];
            message.Encode(buffer, offset);
        }

        [Fact]
        public void PortDecoding()
        {
            EncodeDecode(new PortMessage(5452));
        }


        [Fact]
        public void PortEncoding()
        {
            var length = new PortMessage(2500).Encode(buffer, offset);
            Assert.Equal("00-00-00-03-09-09-C4", BitConverter.ToString(buffer, offset, length));
        }

        [Fact]
        public void RequestDecoding()
        {
            EncodeDecode(new RequestMessage(123, 789, 4235));
        }


        [Fact]
        public void RequestEncoding()
        {
            var length = new RequestMessage(5, 1024, 16384).Encode(buffer, offset);
            Assert.Equal("00-00-00-0D-06-00-00-00-05-00-00-04-00-00-00-40-00",
                BitConverter.ToString(buffer, offset, length));
        }

        [Fact]
        public void UnchokeDecoding()
        {
            EncodeDecode(new UnchokeMessage());
        }


        [Fact]
        public void UnchokeEncoding()
        {
            var length = new UnchokeMessage().Encode(buffer, offset);
            Assert.Equal("00-00-00-01-01", BitConverter.ToString(buffer, offset, length));
        }
    }
}