using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Client;

using NUnit.Framework;

namespace MonoTorrent.PiecePicking
{
    [TestFixture]
    public class StandardPieceRequesterTests
    {
        class TestRequester : IRequester
        {
            public int AmRequestingPiecesCount { get; set; }
            public bool CanRequestMorePieces => CanRequestMorePiecesOverride ();
            public long DownloadSpeed { get; set; }
            public List<int> IsAllowedFastPieces { get; } = new List<int> ();
            public bool IsChoking { get; set; }
            public int MaxPendingRequests { get; set; }
            public int RepeatedHashFails { get; set; }
            public List<int> SuggestedPieces { get; } = new List<int> ();
            public bool SupportsFastPeer { get; set; }
            public bool CanCancelRequests { get; set; }

            public int PreferredRequestAmountOverride { get; set; } = 1;
            public Func<bool> CanRequestMorePiecesOverride = () => true;

            public int PreferredRequestAmount (int pieceLength)
                => PreferredRequestAmountOverride;
        }

        class Enqueuer : IMessageEnqueuer
        {
            public int RequestsEnqueued = 0;
            public void EnqueueCancellation (IRequester peer, PieceSegment segment)
            {
            }

            public void EnqueueCancellations (IRequester peer, Span<PieceSegment> segments)
            {
            }

            public void EnqueueRequest (IRequester peer, PieceSegment block)
            {
                RequestsEnqueued++;
            }

            public void EnqueueRequests (IRequester peer, Span<PieceSegment> blocks)
            {
                RequestsEnqueued += blocks.Length;
            }
        }

        [Test]
        public void CannotRequestMoreThanOne ()
        {
            // Http connections are *supposed* to request data in chunks, and fully complete one
            // chunk before beginning the next. This makes webrequests easier to manage
            var peer = new TestRequester ();
            peer.CanRequestMorePiecesOverride = () => peer.AmRequestingPiecesCount == 0;
            peer.PreferredRequestAmountOverride = 1;
            peer.MaxPendingRequests = 8;

            int pieceCount = 40;
            int pieceLength = 256 * 1024;
            var bitfield = new BitField (pieceCount).SetAll (true);
            var torrentData = TestTorrentManagerInfo.Create (
                files: TorrentFileInfo.Create (pieceLength, ("File", pieceLength * pieceCount, "Full/Path/File")),
                pieceLength: pieceLength,
                size: pieceLength * pieceCount
            );

            var enqueuer = new Enqueuer ();
            var requester = new StandardPieceRequester (new PieceRequesterSettings ());

            requester.Initialise (torrentData, enqueuer, Span<ReadOnlyBitField>.Empty);
            requester.AddRequests (peer, new BitField (bitfield).SetAll (true), Span<ReadOnlyBitField>.Empty);
            Assert.AreEqual (1, peer.AmRequestingPiecesCount);
            Assert.AreEqual (1, enqueuer.RequestsEnqueued);

            requester.AddRequests (peer, new BitField (bitfield).SetAll (true), Span<ReadOnlyBitField>.Empty);
            Assert.AreEqual (1, peer.AmRequestingPiecesCount);
            Assert.AreEqual (1, enqueuer.RequestsEnqueued);
        }
    }
}
