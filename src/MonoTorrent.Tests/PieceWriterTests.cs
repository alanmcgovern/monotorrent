using System;
using System.Collections.Generic;
using NUnit.Framework;
using MonoTorrent.Client;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using System.Threading;
using System.IO;

namespace MonoTorrent.Client.Tests
{
	public class NullWriter : PieceWriter
	{
		PieceWriterTests tester;
		public NullWriter(PieceWriterTests tests)
		{
			tester = tests;
		}
        public override int Read(BufferedIO data)
		{
			Console.WriteLine("Attempting to read - returning zero");
			return 0;
		}

		public override void Write(BufferedIO data)
		{
			Console.WriteLine("Flushed {0}:{1} to disk", data.PieceIndex, data.PieceOffset / 1000);
			tester.blocks.Remove(data);
            ArraySegment<byte> buffer = data.Buffer;
			PieceWriterTests.Buffer.FreeBuffer(ref buffer);
		}

		public override void Close(string path, TorrentFile[] files)
		{
            
		}

        public override void Flush(string path, TorrentFile[] files)
		{

		}
        public override void Flush(string path, TorrentFile[] files, int pieceIndex)
        {
        }
		public override void Dispose()
		{

		}
	}

	[TestFixture]
	public class PieceWriterTests
	{
        static void Main(string[] args)
        {
            PieceWriterTests t = new PieceWriterTests();
            t.GlobalSetup();
            t.Setup();
            t.TestMemoryStandardReads();
        }
		public const int PieceCount = 2;
		public const int BlockCount = 10;
		public const int BlockSize = Piece.BlockSize;
		public const int PieceSize = BlockCount * BlockSize;

		public static BufferManager Buffer = new BufferManager();
		TestRig rig;

		MemoryWriter level1;
		MemoryWriter level2;
        public List<BufferedIO> blocks;

		[TestFixtureSetUp]
		public void GlobalSetup()
		{
			rig = new TestRig("Downloads", PieceSize, null);
		}

		[TestFixtureTearDown]
		public void GlobalTearDown()
		{
			rig.Dispose();
		}

		[SetUp]
		public void Setup()
		{
			blocks = new List<BufferedIO>();
			level2 = new MemoryWriter(new NullWriter(this), (int)(PieceSize * 1.7));
			level1 = new MemoryWriter(level2, (int)(PieceSize * 0.7));

			for (int piece = 0; piece < PieceCount; piece++)
				for (int block = 0; block < BlockCount; block++)
					blocks.Add(CreateBlock(piece, block));
		}

		private BufferedIO CreateBlock(int piece, int block)
		{
			ArraySegment<byte> b = BufferManager.EmptyBuffer;
			Buffer.GetBuffer(ref b, BlockSize);
			for (int i = 0; i < b.Count; i++)
				b.Array[b.Offset + i] = (byte)(piece * BlockCount + block);
			return new BufferedIO(b, piece, block, BlockSize, rig.Manager.Torrent.PieceLength, rig.Manager.Torrent.Files, rig.Manager.FileManager.SavePath);
		}

		[Test]
		public void TestMemoryWrites()
		{
			for (int i = 2; i < 5; i++)
				for (int j = 0; j < BlockCount; j++)
					blocks.Add(CreateBlock(i, j));

			blocks.ForEach(delegate(BufferedIO d) { level1.Write(d); });

			// For the pieces which weren't flushed to the null buffer, make sure they are still accessible
			for (int i = 0; i < blocks.Count; i++)
			{
				ArraySegment<byte> b = blocks[i].Buffer;
				BufferedIO data = blocks[i];
				for (int j = 0; j < b.Count; j++)
					Assert.AreEqual(b.Array[b.Offset + j], data.PieceIndex * BlockCount + data.PieceOffset / data.Count, "#1");
			}
		}

		[Test]
		public void TestMemoryStandardReads()
		{
			ArraySegment<byte> buffer = BufferManager.EmptyBuffer;
			Buffer.GetBuffer(ref buffer, BlockSize);
			Initialise(buffer);
			foreach (BufferedIO data in this.blocks.ToArray())
				level1.Write(data);

			for (int piece=0; piece < PieceCount; piece++)
			{
				for(int block = 0; block < BlockCount; block++)
				{
					BufferedIO io = new BufferedIO(buffer, piece, block, BlockSize, rig.Manager.Torrent.PieceLength, rig.Manager.Torrent.Files, rig.Manager.FileManager.SavePath);
					level1.ReadChunk(io);

					for (int i = 0; i < BlockSize; i++)
						Assert.AreEqual(buffer.Array[buffer.Offset + i], piece * BlockCount + block, "#1");
				}
			}
		}

		[Test]
		public void TestMemoryOffsetReads()
		{
			level1.Write(blocks[0]);
			level2.Write(blocks[1]);
			level1.Write(blocks[2]);
			level2.Write(blocks[3]);
			level2.Write(blocks[4]);

			ArraySegment<byte> buffer = BufferManager.EmptyBuffer;
			Buffer.GetBuffer(ref buffer, PieceSize);
			Initialise(buffer);

			int piece = 0;
            int block = 0;

			BufferedIO io = new BufferedIO(buffer, piece, block, PieceSize, rig.Manager.Torrent.PieceLength, rig.Manager.Torrent.Files, rig.Manager.FileManager.SavePath);
			level1.ReadChunk(io);
			for (block = 0; block < 5; block++)
			{
				for (int i = 0; i < BlockSize; i++)
					Assert.AreEqual(block, buffer.Array[buffer.Offset + i + block * BlockSize], "Piece 0. Block " + i);
			}
		}

		private void Initialise(ArraySegment<byte> buffer)
		{
			for (int i = 0; i < buffer.Count; i++)
				buffer.Array[buffer.Offset + i] = 0;
		}
	}
}
