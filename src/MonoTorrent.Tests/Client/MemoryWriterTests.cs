//
// DiskWriterTests.cs
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


using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoTorrent.Client.PieceWriters
{
	public class MemoryWriterTests
	{
        byte[] buffer;
        MemoryWriter level1;
        MemoryWriter level2;

        TorrentFile singleFile => singleTorrentManager.Torrent.Files [0];
        TorrentManager singleTorrentManager;

        TorrentFile[] multiFile => multiTorrentManager.Torrent.Files;
        TorrentManager multiTorrentManager;

		[SetUp]
		public void Setup()
		{
            var pieceLength = Piece.BlockSize * 2;

            singleTorrentManager = TestRig.CreateSingleFileManager (Piece.BlockSize * 5, pieceLength);
            multiTorrentManager = TestRig.CreateMultiFileManager (new TorrentFile[] {
                new TorrentFile ("first", Piece.BlockSize - 550),
                new TorrentFile ("second", 100),
                new TorrentFile ("third", Piece.BlockSize)
            }, pieceLength);
            buffer = new byte[Piece.BlockSize];

            Initialise(buffer, 1);
			level2 = new MemoryWriter(new NullWriter(), Piece.BlockSize * 3);
            level1 = new MemoryWriter(level2, Piece.BlockSize * 3);
		}

        [Test]
        public void FillFirstBuffer()
        {
            // Write 4 blocks to the stream and then verify they can all be read
            for (int i = 0; i < 4; i++)
            {
                Initialise(buffer, (byte)(i + 1));
                level1.Write(singleFile, Piece.BlockSize * i, buffer, 0, buffer.Length);
            }

            // Read them all back out and verify them
            for (int i = 0; i < 4; i++)
            {
                level1.Read(singleFile, Piece.BlockSize * i, buffer, 0, Piece.BlockSize);
                Verify(buffer, (byte)(i + 1));
            }
        }

        [Test]
        public void ReadWriteBlock()
        {
            level1.Write(singleFile, 0, buffer, 0, buffer.Length);
            level1.Read(singleFile, 0, buffer, 0, buffer.Length);
            Verify(buffer, 1);
        }

        [Test]
        public void ReadWriteBlockChangeOriginal()
        {
            level1.Write(singleFile, 0, buffer, 0, buffer.Length);
            Initialise(buffer, 5);
            level1.Read(singleFile, 0, buffer, 0, buffer.Length);
            Verify(buffer, 1);
        }

        [Test]
        public async Task ReadWriteSpanningBlock()
        {
            // Write one block of data to the memory stream. 
            int file1 = (int)multiFile[0].Length;
            int file2 = (int)multiFile[1].Length;
            int file3 = Piece.BlockSize - file1 - file2;

            Initialise(buffer, 1);
            level1.Write(multiFile[0], 0, buffer, 0, file1);

            Initialise(buffer, 2);
            level1.Write(multiFile[1], 0, buffer, 0, file2);

            Initialise(buffer, 3);
            level1.Write(multiFile[2], 0, buffer, 0, file3);

            // Read the block from the memory stream
            var manager = new DiskManager (new EngineSettings (), level1);
            await manager.ReadAsync(multiTorrentManager, 0, buffer, Piece.BlockSize);

            // Ensure that the data is in the buffer exactly as expected.
            Verify(buffer, 0, file1, 1);
            Verify(buffer, file1, file2, 2);
            Verify(buffer, file1 + file2, file3, 3);
        }
        
        void Initialise(byte[] buffer, byte value)
		{
			for (int i = 0; i < buffer.Length; i++)
				buffer[i] = value;
		}

        void Verify(byte[] buffer, byte expected)
        {
            Verify(buffer, 0, buffer.Length, expected);
        }

        void Verify(byte[] buffer, int startOffset, int count, byte expected)
        {
            for (int i = startOffset; i < startOffset + count; i++)
                Assert.AreEqual(buffer[i], expected, "#" + i);
        }
	}
}
