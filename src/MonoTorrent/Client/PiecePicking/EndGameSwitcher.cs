using System;
using System.Collections.Generic;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class EndGameSwitcher : PiecePicker
    {
        private const int Threshold = 20;

        private BitField bitfield;
        private readonly int blocksPerPiece;
        private readonly PiecePicker endgame;
        private BitField endgameSelector;
        private TorrentFile[] files;
        private bool inEndgame;
        private readonly PiecePicker standard;
        private readonly TorrentManager torrentManager;

        public EndGameSwitcher(StandardPicker standard, EndGamePicker endgame, int blocksPerPiece,
            TorrentManager torrentManager)
            : base(null)
        {
            this.standard = standard;
            this.endgame = endgame;
            this.blocksPerPiece = blocksPerPiece;
            this.torrentManager = torrentManager;
        }

        private PiecePicker ActivePicker
        {
            get { return inEndgame ? endgame : standard; }
        }

        public override void CancelRequest(PeerId peer, int piece, int startOffset, int length)
        {
            ActivePicker.CancelRequest(peer, piece, startOffset, length);
        }

        public override void CancelRequests(PeerId peer)
        {
            ActivePicker.CancelRequests(peer);
        }

        public override void CancelTimedOutRequests()
        {
            ActivePicker.CancelTimedOutRequests();
        }

        public override RequestMessage ContinueExistingRequest(PeerId peer)
        {
            return ActivePicker.ContinueExistingRequest(peer);
        }

        public override int CurrentRequestCount()
        {
            return ActivePicker.CurrentRequestCount();
        }

        public override List<Piece> ExportActiveRequests()
        {
            return ActivePicker.ExportActiveRequests();
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            this.bitfield = bitfield;
            endgameSelector = new BitField(bitfield.Length);
            this.files = files;
            inEndgame = false;
            TryEnableEndgame();
            ActivePicker.Initialise(bitfield, files, requests);
        }

        public override bool IsInteresting(BitField bitfield)
        {
            return ActivePicker.IsInteresting(bitfield);
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count,
            int startIndex, int endIndex)
        {
            var bundle = ActivePicker.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);
            if (bundle == null && TryEnableEndgame())
                return ActivePicker.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);
            return bundle;
        }

        private bool TryEnableEndgame()
        {
            if (inEndgame)
                return false;

            // We need to activate endgame mode when there are less than 20 requestable blocks
            // available. We OR the bitfields of all the files which are downloadable and then
            // NAND it with the torrents bitfield to get a list of pieces which remain to be downloaded.

            // Essentially we get a list of all the pieces we're allowed download, then get a list of
            // the pieces which we still need to get and AND them together.

            // Create the bitfield of pieces which are downloadable
            endgameSelector.SetAll(false);
            for (var i = 0; i < files.Length; i++)
                if (files[i].Priority != Priority.DoNotDownload)
                    endgameSelector.Or(files[i].GetSelector(bitfield.Length));

            // NAND it with the pieces we already have (i.e. AND it with the pieces we still need to receive)
            endgameSelector.NAnd(bitfield);

            // If the total number of blocks remaining is less than Threshold, activate Endgame mode.
            var count = 0;
            var pieces = standard.ExportActiveRequests();
            for (var i = 0; i < pieces.Count; i++)
                count += pieces[i].TotalReceived;
            inEndgame = Math.Max(blocksPerPiece, endgameSelector.TrueCount*blocksPerPiece) - count < Threshold;
            if (inEndgame)
            {
                endgame.Initialise(bitfield, files, standard.ExportActiveRequests());
                // Set torrent's IsInEndGame flag
                torrentManager.isInEndGame = true;
            }
            return inEndgame;
        }

        public override void Reset()
        {
            inEndgame = false;
            torrentManager.isInEndGame = false;
            standard.Reset();
            endgame.Reset();
        }

        public override bool ValidatePiece(PeerId peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            return ActivePicker.ValidatePiece(peer, pieceIndex, startOffset, length, out piece);
        }
    }
}