using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Client
{
    public class MemoryWriterTests
    {
        public MemoryWriterTests()
        {
            pieceLength = Piece.BlockSize*2;
            singleFile = new TorrentFile("path", Piece.BlockSize*5);
            multiFile = new[]
            {
                new TorrentFile("first", Piece.BlockSize - 550),
                new TorrentFile("second", 100),
                new TorrentFile("third", Piece.BlockSize)
            };
            buffer = new byte[Piece.BlockSize];
            torrentSize = Toolbox.Accumulate(multiFile, delegate(TorrentFile f) { return f.Length; });

            Initialise(buffer, 1);
            level2 = new MemoryWriter(new NullWriter(), Piece.BlockSize*3);
            level1 = new MemoryWriter(level2, Piece.BlockSize*3);
        }

        private readonly byte[] buffer;
        private readonly MemoryWriter level1;
        private readonly MemoryWriter level2;

        private readonly TorrentFile singleFile;
        private readonly TorrentFile[] multiFile;

        private readonly int pieceLength;
        private readonly long torrentSize;

        private void Initialise(byte[] buffer, byte value)
        {
            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = value;
        }

        private void Verify(byte[] buffer, byte expected)
        {
            Verify(buffer, 0, buffer.Length, expected);
        }

        private void Verify(byte[] buffer, int startOffset, int count, byte expected)
        {
            for (var i = startOffset; i < startOffset + count; i++)
                Assert.Equal(buffer[i], expected);
        }

        [Fact]
        public void FillFirstBuffer()
        {
            // Write 4 blocks to the stream and then verify they can all be read
            for (var i = 0; i < 4; i++)
            {
                Initialise(buffer, (byte) (i + 1));
                level1.Write(singleFile, Piece.BlockSize*i, buffer, 0, buffer.Length);
            }

            // Read them all back out and verify them
            for (var i = 0; i < 4; i++)
            {
                level1.Read(singleFile, Piece.BlockSize*i, buffer, 0, Piece.BlockSize);
                Verify(buffer, (byte) (i + 1));
            }
        }

        [Fact]
        public void ReadWriteBlock()
        {
            level1.Write(singleFile, 0, buffer, 0, buffer.Length);
            level1.Read(singleFile, 0, buffer, 0, buffer.Length);
            Verify(buffer, 1);
        }

        [Fact]
        public void ReadWriteBlockChangeOriginal()
        {
            level1.Write(singleFile, 0, buffer, 0, buffer.Length);
            Initialise(buffer, 5);
            level1.Read(singleFile, 0, buffer, 0, buffer.Length);
            Verify(buffer, 1);
        }

        [Fact]
        public void ReadWriteSpanningBlock()
        {
            // Write one block of data to the memory stream. 
            var file1 = (int) multiFile[0].Length;
            var file2 = (int) multiFile[1].Length;
            var file3 = Piece.BlockSize - file1 - file2;

            Initialise(buffer, 1);
            level1.Write(multiFile[0], 0, buffer, 0, file1);

            Initialise(buffer, 2);
            level1.Write(multiFile[1], 0, buffer, 0, file2);

            Initialise(buffer, 3);
            level1.Write(multiFile[2], 0, buffer, 0, file3);

            // Read the block from the memory stream
            level1.Read(multiFile, 0, buffer, 0, Piece.BlockSize, pieceLength, torrentSize);

            // Ensure that the data is in the buffer exactly as expected.
            Verify(buffer, 0, file1, 1);
            Verify(buffer, file1, file2, 2);
            Verify(buffer, file1 + file2, file3, 3);
        }
    }
}