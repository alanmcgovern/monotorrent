using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Messages.Peer;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class MerkleTests
    {
        static string V2OnlyTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (MerkleTests).Assembly.Location), "MonoTorrent", "bittorrent-v2-test.torrent");

        Torrent V2OnlyTorrent { get; } = Torrent.Load (V2OnlyTorrentPath);

        ReadOnlyMerkleLayers CreateMerkleTree (int pieceLength, MerkleRoot expectedRoot, ReadOnlySpan<byte> layerHashes)
            => ReadOnlyMerkleLayers.FromLayer (pieceLength, expectedRoot, layerHashes);

        [Test]
        public void CreateTree_OneRootHash ([Values (1)] int hashes)
        {
            var expectedRoot = MerkleRoot.FromMemory (MerkleHash.PaddingHashesByLayer[1]);
            var leafHashes = Replicate (MerkleHash.PaddingHashesByLayer[0], hashes);

            Assert.Throws<ArgumentException> (() => CreateMerkleTree (Constants.BlockSize, expectedRoot, leafHashes.Span));
        }

        [Test]
        public void CreateTree_2Hashes ([Values(2)] int hashes)
        {
            var expectedRoot = MerkleRoot.FromMemory (MerkleHash.PaddingHashesByLayer[BitOps.CeilLog2 (Constants.BlockSize * 2)]);
            var leafHashes = Replicate (MerkleHash.PaddingHashesByLayer[BitOps.CeilLog2 (Constants.BlockSize)], hashes);

            var layers = CreateMerkleTree (Constants.BlockSize, expectedRoot, leafHashes.Span);
            Contains (layers, 0, MerkleHash.PaddingHashesByLayer[BitOps.CeilLog2 (Constants.BlockSize)], 2);
            Contains (layers, 1, MerkleHash.PaddingHashesByLayer[BitOps.CeilLog2 (Constants.BlockSize * 2)], 1);
        }

        [Test]
        public void CreateTree_4Hashes ([Values (3, 4)] int hashes)
        {
            var expectedRoot = MerkleRoot.FromMemory (MerkleHash.PaddingHashesByLayer[2]);
            var leafHashes = Replicate (MerkleHash.PaddingHashesByLayer[0], hashes);

            var layers = CreateMerkleTree (Constants.BlockSize, expectedRoot, leafHashes.Span);
            Contains (layers, 0, MerkleHash.PaddingHashesByLayer[0], 4);
            Contains (layers, 1, MerkleHash.PaddingHashesByLayer[1], 2);
            Contains (layers, 2, MerkleHash.PaddingHashesByLayer[2], 1);
        }

        [Test]
        public void CreateTree_8Hashes ([Values (5, 6, 7, 8)] int hashes)
        {
            var expectedRoot = MerkleRoot.FromMemory (MerkleHash.PaddingHashesByLayer[3]);
            var leafHashes = Replicate (MerkleHash.PaddingHashesByLayer[0], hashes);

            var layers = CreateMerkleTree (Constants.BlockSize, expectedRoot, leafHashes.Span);
            Contains (layers, 0, MerkleHash.PaddingHashesByLayer[0], 8);
            Contains (layers, 1, MerkleHash.PaddingHashesByLayer[1], 4);
            Contains (layers, 2, MerkleHash.PaddingHashesByLayer[2], 2);
            Contains (layers, 3, MerkleHash.PaddingHashesByLayer[3], 1);
        }

        [Test]
        public void ReplicateMerkleTree ([Values (2, 4, 8, 16, 32, 64, 512)] int requestSize)
        {
            var originalHashes = V2OnlyTorrent.CreatePieceHashes ();

            var result = new Dictionary<MerkleRoot, ReadOnlyMerkleLayers> ();
            foreach (var file in V2OnlyTorrent.Files.Where (t => t.EndPieceIndex > t.StartPieceIndex)) {
                var piecesInFile = file.EndPieceIndex - file.StartPieceIndex + 1;
                var currentFileLayers = new MerkleLayers (file.PiecesRoot, V2OnlyTorrent.PieceLength, piecesInFile);
                for (int i = 0; i < piecesInFile; i += requestSize) {
                    var requestMessage = HashRequestMessage.Create (file.PiecesRoot, piecesInFile, V2OnlyTorrent.PieceLength, i, requestSize);
                    var hashBytesRequested = requestMessage.Length * 32;
                    var proofBytesRequested = requestMessage.ProofLayers * 32;
                    Memory<byte> buffer = new byte[hashBytesRequested + proofBytesRequested];
                    Assert.IsTrue (originalHashes.TryGetV2Hashes (file.PiecesRoot, requestMessage.BaseLayer, i, requestMessage.Length, buffer.Span.Slice (0, hashBytesRequested), buffer.Span.Slice (hashBytesRequested, proofBytesRequested), out int actualProofLayers));
                    Assert.IsTrue (currentFileLayers.TryAppend (requestMessage.BaseLayer, i, requestMessage.Length, buffer.Span.Slice (0, hashBytesRequested), buffer.Span.Slice (hashBytesRequested, actualProofLayers * 32)));
                }
                if (currentFileLayers.TryVerify (out var verifiedTree))
                    result.Add (file.PiecesRoot, verifiedTree);
            }

            var newHashes = new PieceHashesV2 (V2OnlyTorrent.PieceLength, V2OnlyTorrent.Files, result);
            for (int i = 0; i < originalHashes.Count; i++)
                Assert.IsTrue (newHashes.IsValid (originalHashes.GetHash (i), i));
        }

        static ReadOnlyMemory<byte> Replicate (ReadOnlyMemory<byte> hash, int count)
            => Enumerable.Repeat (hash.ToArray (), count)
                         .SelectMany (t => t)
                         .ToArray ();

        static void Contains (ReadOnlyMerkleLayers layers, int layerIndex, ReadOnlyMemory<byte> expectedHash, int expectedCount)
        {
            for (int i = 0; i < expectedCount; i++)
                layers.GetHash (layerIndex, i).Span.SequenceEqual (expectedHash.Span);
        }
    }
}
