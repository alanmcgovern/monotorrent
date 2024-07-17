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
        static string HybridTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (MerkleTests).Assembly.Location), "MonoTorrent", "bittorrent-v2-hybrid-test.torrent");
        static string V2OnlyTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (MerkleTests).Assembly.Location), "MonoTorrent", "bittorrent-v2-test.torrent");

        Torrent HybridTorrent { get; } = Torrent.Load (HybridTorrentPath);
        Torrent V2OnlyTorrent { get; } = Torrent.Load (V2OnlyTorrentPath);

        ReadOnlyMerkleTree CreateMerkleTree (int pieceLength, MerkleRoot expectedRoot, ReadOnlySpan<byte> layerHashes)
            => ReadOnlyMerkleTree.FromLayer (pieceLength, layerHashes, expectedRoot);

        [Test]
        public void CreateTree_OneRootHash ([Values (1)] int hashes)
        {
            var expectedRoot = MerkleRoot.FromMemory (MerkleTreeHasher.PaddingHashesByLayer[1]);
            var leafHashes = Replicate (MerkleTreeHasher.PaddingHashesByLayer[0], hashes);

            Assert.Throws<ArgumentException> (() => CreateMerkleTree (Constants.BlockSize, expectedRoot, leafHashes.Span));
        }

        [Test]
        public void CreateTree_2Hashes ([Values (2)] int hashes)
        {
            var expectedRoot = MerkleRoot.FromMemory (MerkleTreeHasher.PaddingHashesByLayer[BitOps.CeilLog2 (Constants.BlockSize * 2)]);
            var leafHashes = Replicate (MerkleTreeHasher.PaddingHashesByLayer[BitOps.CeilLog2 (Constants.BlockSize)], hashes);

            var layers = CreateMerkleTree (Constants.BlockSize, expectedRoot, leafHashes.Span);
            Contains (layers, 0, MerkleTreeHasher.PaddingHashesByLayer[BitOps.CeilLog2 (Constants.BlockSize)], 2);
            Contains (layers, 1, MerkleTreeHasher.PaddingHashesByLayer[BitOps.CeilLog2 (Constants.BlockSize * 2)], 1);
        }

        [Test]
        public void CreateTree_4Hashes ([Values (3, 4)] int hashes)
        {
            var expectedRoot = MerkleRoot.FromMemory (MerkleTreeHasher.PaddingHashesByLayer[2]);
            var leafHashes = Replicate (MerkleTreeHasher.PaddingHashesByLayer[0], hashes);

            var layers = CreateMerkleTree (Constants.BlockSize, expectedRoot, leafHashes.Span);
            Contains (layers, 0, MerkleTreeHasher.PaddingHashesByLayer[0], 4);
            Contains (layers, 1, MerkleTreeHasher.PaddingHashesByLayer[1], 2);
            Contains (layers, 2, MerkleTreeHasher.PaddingHashesByLayer[2], 1);
        }

        [Test]
        public void CreateTree_8Hashes ([Values (5, 6, 7, 8)] int hashes)
        {
            var expectedRoot = MerkleRoot.FromMemory (MerkleTreeHasher.PaddingHashesByLayer[3]);
            var leafHashes = Replicate (MerkleTreeHasher.PaddingHashesByLayer[0], hashes);

            var layers = CreateMerkleTree (Constants.BlockSize, expectedRoot, leafHashes.Span);
            Contains (layers, 0, MerkleTreeHasher.PaddingHashesByLayer[0], 8);
            Contains (layers, 1, MerkleTreeHasher.PaddingHashesByLayer[1], 4);
            Contains (layers, 2, MerkleTreeHasher.PaddingHashesByLayer[2], 2);
            Contains (layers, 3, MerkleTreeHasher.PaddingHashesByLayer[3], 1);
        }

        [Test]
        public void ReplicateMerkleTree_FixedChunks_Hybrid ([Values (2, 4, 8, 16, 32, 64, 512)] int preferredRequestSize)
            => ReplicateMerkleTree_FixedChunks(HybridTorrent, preferredRequestSize);

        [Test]
        public void ReplicateMerkleTree_FixedChunks_V2Only ([Values (2, 4, 8, 16, 32, 64, 512)] int preferredRequestSize)
            => ReplicateMerkleTree_FixedChunks (V2OnlyTorrent, preferredRequestSize);

        static void ReplicateMerkleTree_FixedChunks (Torrent torrent,int preferredRequestSize)
        {
            var originalHashes = torrent.CreatePieceHashes ();

            var result = new Dictionary<MerkleRoot, ReadOnlyMerkleTree> ();
            foreach (var file in torrent.Files.Where (t => t.PieceCount > 1)) {
                // Create the merkle layer with the precise number of pieces
                var currentFileLayers = new MerkleTree (file.PiecesRoot, torrent.PieceLength, file.PieceCount);

                // But we always round up to the layer size when making requests. Layers are always a power of 2.
                var piecesInLayer = (int) BitOps.RoundUpToPowerOf2 (file.PieceCount);

                // However if we try to request 512 hashes for a file which is only 17 hashes long, clamp to the size of
                // the pieces layer (i.e. the 32 hash layer in this case)
                var requestSize = Math.Min (preferredRequestSize, piecesInLayer);

                for (int i = 0; i < file.PieceCount; i += requestSize) {
                    var requestMessage = HashRequestMessage.CreateFromPieceLayer (file.PiecesRoot, file.PieceCount, torrent.PieceLength, i, requestSize);
                    var hashBytesRequested = (requestMessage.Length + requestMessage.ProofLayers) * 32;
                    Memory<byte> buffer = new byte[hashBytesRequested];
                    Assert.IsTrue (originalHashes.TryGetV2Hashes (file.PiecesRoot, requestMessage.BaseLayer, requestMessage.Index, requestMessage.Length, requestMessage.ProofLayers, buffer.Span, out int bytesWritten));
                    Assert.IsTrue (currentFileLayers.TryAppend (requestMessage.BaseLayer, requestMessage.Index, requestMessage.Length, buffer.Span.Slice (0, bytesWritten)));
                }
                if (currentFileLayers.TryVerify (out var verifiedTree))
                    result.Add (file.PiecesRoot, verifiedTree);
                else
                    Assert.Fail ("Hash did not match");
            }

            var newHashes = new PieceHashesV2 (torrent.PieceLength, torrent.Files, result);
            for (int i = 0; i < originalHashes.Count; i++)
                Assert.IsTrue (newHashes.IsValid (originalHashes.GetHash (i), i));
        }

        [Test]
        public void ReplicateMerkleTree_OptimalChunks_27551708 ()
        {
            const int PieceLength = 4 * 1024 * 1024;
            var msg = HashRequestMessage.CreateFromPieceLayer (MerkleRoot.Empty, 7, PieceLength, 0, null);
            Assert.AreEqual (new HashRequestMessage (MerkleRoot.Empty, 8, 0, 8, 2), msg);

            msg = HashRequestMessage.CreateFromPieceLayer (MerkleRoot.Empty, 22, PieceLength, 0, null);
            Assert.AreEqual (new HashRequestMessage (MerkleRoot.Empty, 8, 0, 32, 4), msg);

            msg = HashRequestMessage.CreateFromPieceLayer (MerkleRoot.Empty, 2, PieceLength, 0, null);
            Assert.AreEqual (new HashRequestMessage (MerkleRoot.Empty, 8, 0, 2, 0), msg);

            msg = HashRequestMessage.CreateFromPieceLayer (MerkleRoot.Empty, 34, PieceLength, 0, null);
            Assert.AreEqual (new HashRequestMessage (MerkleRoot.Empty, 8, 0, 64, 5), msg);

            msg = HashRequestMessage.CreateFromPieceLayer (MerkleRoot.Empty, 63, PieceLength, 0, null);
            Assert.AreEqual (new HashRequestMessage (MerkleRoot.Empty, 8, 0, 64, 5), msg);

            msg = HashRequestMessage.CreateFromPieceLayer (MerkleRoot.Empty, 82, PieceLength, 0, null);
            Assert.AreEqual (new HashRequestMessage (MerkleRoot.Empty, 8, 0, 128, 6), msg);
        }


        static ReadOnlyMemory<byte> Replicate (ReadOnlyMemory<byte> hash, int count)
            => Enumerable.Repeat (hash.ToArray (), count)
                         .SelectMany (t => t)
                         .ToArray ();

        static void Contains (ReadOnlyMerkleTree layers, int layerIndex, ReadOnlyMemory<byte> expectedHash, int expectedCount)
        {
            for (int i = 0; i < expectedCount; i++)
                layers.GetHash (layerIndex, i).Span.SequenceEqual (expectedHash.Span);
        }
    }
}
