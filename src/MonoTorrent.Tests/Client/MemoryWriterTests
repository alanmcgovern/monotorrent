using System;
using System.Collections.Generic;
using NUnit.Framework;
using MonoTorrent.Client;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using System.Threading;
using System.IO;

namespace MonoTorrent.Client
{
	public class NullWriter : PieceWriter
	{
		PieceWriterTests tester;
		public NullWriter(PieceWriterTests tests)
		{
			tester = tests;
		}
        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            return 0;
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            //tester.blocks.RemoveAll(delegate (BufferedIO io) {
            //    return io.Offset == offset && io.Count == count;
            //});
        }

		public override void Close(TorrentFile file)
		{
            
		}

        public override void Flush(TorrentFile file)
		{

		}
		public override void Dispose()
		{

		}

        public override bool Exists(TorrentFile file)
        {
            return false;
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
            
        }
    }

	[TestFixture(Ignore=true)]
	public class PieceWriterTests
	{
        //static void Main(string[] args)
        //{
        //    PieceWriterTests t = new PieceWriterTests();
        //    t.GlobalSetup();
        //    t.Setup();
        //    t.TestMemoryStandardReads();
        //}
        public static readonly int PieceCount = 2;
        public static readonly int BlockCount = 10;
		public static readonly int BlockSize = Piece.BlockSize;
        public static readonly int PieceSize = BlockCount * BlockSize;

		public static BufferManager Buffer = new BufferManager();
		TestRig rig;

		MemoryWriter level1;
		MemoryWriter level2;
        //public List<BufferedIO> blocks;

		[TestFixtureSetUp]
		public void GlobalSetup()
		{
			rig = TestRig.CreateMultiFile (PieceSize);
		}

		[TestFixtureTearDown]
		public void GlobalTearDown()
		{
			rig.Dispose();
		}

		[SetUp]
		public void Setup()
		{
			//blocks = new List<BufferedIO>();
			level2 = new MemoryWriter(new NullWriter(this), (int)(PieceSize * 1.7));
			level1 = new MemoryWriter(level2, (int)(PieceSize * 0.7));

			//for (int piece = 0; piece < PieceCount; piece++)
			//	for (int block = 0; block < BlockCount; block++)
			//		blocks.Add(CreateBlock(piece, block));
		}

		//private BufferedIO CreateBlock(int piece, int block)
		//{
			//ArraySegment<byte> b = BufferManager.EmptyBuffer;
			//Buffer.GetBuffer(ref b, BlockSize);
			//for (int i = 0; i < b.Count; i++)
			//	b.Array[b.Offset + i] = (byte)(piece * BlockCount + block);
			//BufferedIO io = new BufferedIO();
            //io.Initialise(rig.Manager, b, piece, block, BlockSize, rig.Manager.Torrent.PieceLength, rig.Manager.Torrent.Files);
            //return io;
        //}

		[Test]
        [Ignore]
		public void TestMemoryWrites()
		{
			//for (int i = 2; i < 5; i++)
			//	for (int j = 0; j < BlockCount; j++)
			//		blocks.Add(CreateBlock(i, j));

			//blocks.ForEach(delegate(BufferedIO d) { level1.Write(d.Files, d.Offset, d.buffer.Array, d.buffer.Offset, d.Count, d.PieceLength, d.Manager.Torrent.Size); });

			// For the pieces which weren't flushed to the null buffer, make sure they are still accessible
			//for (int i = 0; i < blocks.Count; i++)
			//{
			//	ArraySegment<byte> b = blocks[i].Buffer;
			//	BufferedIO data = blocks[i];
			//	for (int j = 0; j < b.Count; j++)
			//		Assert.AreEqual(b.Array[b.Offset + j], data.PieceIndex * BlockCount + data.PieceOffset / data.Count, "#1");
			//}
		}

		[Test]
        [Ignore]
		public void TestMemoryStandardReads()
		{
			//ArraySegment<byte> buffer = BufferManager.EmptyBuffer;
            //level2.Capacity = PieceSize * 20;
			//Buffer.GetBuffer(ref buffer, BlockSize);
			//Initialise(buffer);
			//foreach (BufferedIO data in this.blocks.ToArray())
			//	level1.WriteBlock(data);

			//for (int piece=0; piece < PieceCount; piece++)
			//{
			//	for(int block = 0; block < BlockCount; block++)
			//	{
            //       level1.ReadBlock(rig.Manager.Torrent.Files, piece, block, buffer.Array, buffer.Offset, rig.Manager.Torrent.PieceLength, rig.Manager.Torrent.Size);
            //
			//		for (int i = 0; i < BlockSize; i++)
			//			Assert.AreEqual(buffer.Array[buffer.Offset + i], piece * BlockCount + block, "#1");
			//	}
			//}
		}

		[Test]
        [Ignore]
		public void TestMemoryOffsetReads()
		{
            //level1.WriteBlock(blocks[0]);
            //level2.WriteBlock(blocks[1]);
            //level1.WriteBlock(blocks[2]);
            //level2.WriteBlock(blocks[3]);
            //level2.WriteBlock(blocks[4]);

			//ArraySegment<byte> buffer = BufferManager.EmptyBuffer;
			//Buffer.GetBuffer(ref buffer, PieceSize);
			//Initialise(buffer);

			//int piece = 0;
            //int block = 0;

			//level1.ReadPiece(rig.Manager.Torrent.Files, piece, buffer.Array, buffer.Offset, PieceSize, rig.Manager.Torrent.Size);
			//for (block = 0; block < 5; block++)
			//{
			//	for (int i = 0; i < BlockSize; i++)
			//		Assert.AreEqual(block, buffer.Array[buffer.Offset + i + block * BlockSize], "Piece 0. Block " + i);
			//}
		}

		private void Initialise(ArraySegment<byte> buffer)
		{
			for (int i = 0; i < buffer.Count; i++)
				buffer.Array[buffer.Offset + i] = 0;
		}
	}
}
