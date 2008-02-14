using System;
using System.Collections.Generic;
using NUnit.Framework;
using MonoTorrent.Client;
using MonoTorrent.Client.PieceWriter;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Tests
{
	public class NullWriter : IPieceWriter
	{
		PieceWriterTests tester;
		public NullWriter(PieceWriterTests tests)
		{
			tester = tests;
		}

		public int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count)
		{
			return 0;
		}

		public void Write(PieceData data)
		{
			tester.blocks.Remove(data);
			Console.WriteLine("Flushed through to the null");
			PieceWriterTests.Buffer.FreeBuffer(ref data.Buffer);
		}

		public void CloseFileStreams(TorrentManager manager)
		{

		}

		public void Flush(TorrentManager manager)
		{

		}

		public void Dispose()
		{

		}
	}

	[TestFixture]
	public class PieceWriterTests
	{
		public static BufferManager Buffer = new BufferManager();
		SampleClient.EngineTestRig rig;

		MemoryWriter level1;
		MemoryWriter level2;
		public List<PieceData> blocks;

		[SetUp]
		public void Setup()
		{
			rig = new SampleClient.EngineTestRig("Downloads", 10000);

			blocks = new List<PieceData>();
			level2 = new MemoryWriter(new NullWriter(this), 50000);
			level1 = new MemoryWriter(level2, 50000);

			for (int piece = 0; piece < 2; piece++)
			{
				for (int block = 0; block < 5; block++)
				{
					PieceData d = CreateBlock(piece, block);
					blocks.Add(d);
					level1.Write(d);
				}
			}
		}

		private PieceData CreateBlock(int piece, int block)
		{
			ArraySegment<byte> b = BufferManager.EmptyBuffer;
			Buffer.GetBuffer(ref b, 1000);
			for (int i = 0; i < b.Count; i++)
				b.Array[b.Offset + i] = (byte)(piece * 10 + block);
			return new PieceData(b, piece, block * 1000, 1000, rig.Manager.FileManager);
		}

		[Test]
		public void TestMemoryWrites()
		{
			// Generate a load of pieces and flush them through the double buffer
			for (int piece = 0; piece < 6; piece++)
			{
				for (int block = 0; block < 10; block++)
				{
					PieceData d = CreateBlock(piece, block);
					blocks.Add(d);
					level1.Write(d);
				}
			}

			// For the pieces which weren't flushed to the null buffer, make sure they weren't overwritten incorrectly
			for (int i = 0; i < blocks.Count; i++)
			{
				ArraySegment<byte> b = blocks[i].Buffer;
				PieceData data = blocks[i];
				for (int j = 0; j < b.Count; j++)
					Assert.AreEqual(b.Array[b.Offset + j], data.PieceIndex * 10 + data.StartOffset / data.Count, "#1");
			}
		}

		[Test]
		public void TestMemoryStandardReads()
		{

			ArraySegment<byte> buffer = BufferManager.EmptyBuffer;
			Buffer.GetBuffer(ref buffer, 1000);
			Initialise(buffer);
			for (int piece=0; piece < 2; piece++)
			{
				for(int block = 0; block < 5; block++)
				{
					long readIndex = (long)piece * rig.Manager.Torrent.PieceLength + block * 1000;
					level1.Read(rig.Manager.FileManager, buffer.Array, buffer.Offset, readIndex, 1000);

					for (int i = 0; i < 1000; i++)
						Assert.AreEqual(buffer.Array[buffer.Offset + i], piece * 10 + block, "#1");
				}
			}
		}


		[Test]
		public void TestMemoryOffsetReads()
		{

			ArraySegment<byte> buffer = BufferManager.EmptyBuffer;
			Buffer.GetBuffer(ref buffer, 10000);
			Initialise(buffer);
			int piece = 0;
			int block = 0;

			long readIndex = (long)piece * rig.Manager.Torrent.PieceLength + block * 1000;
			level1.Read(rig.Manager.FileManager, buffer.Array, buffer.Offset, readIndex, 10000);

			for (int i = 0; i < 1000; i++)
				Assert.AreEqual(buffer.Array[buffer.Offset + i], 0, "#0");
			for (int i = 0; i < 1000; i++)
				Assert.AreEqual(buffer.Array[buffer.Offset + i], 1, "#1");
			for (int i = 0; i < 1000; i++)
				Assert.AreEqual(buffer.Array[buffer.Offset + i], 2, "#2");
			for (int i = 0; i < 1000; i++)
				Assert.AreEqual(buffer.Array[buffer.Offset + i], 3, "#3");
			for (int i = 0; i < 1000; i++)
				Assert.AreEqual(buffer.Array[buffer.Offset + i], 4, "#4");
		}

		private void Initialise(ArraySegment<byte> buffer)
		{
			for (int i = 0; i < buffer.Count; i++)
				buffer.Array[buffer.Offset + i] = 0;
		}

		public static void Main(string[] args)
		{
			PieceWriterTests t = new PieceWriterTests();
			t.Setup();
			t.TestMemoryOffsetReads();
		}
	}
}
