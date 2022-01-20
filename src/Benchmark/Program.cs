using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Messages.Peer;
using MonoTorrent.PiecePicking;

namespace MyBenchmarks
{
    [MemoryDiagnoser]
    public class BitfieldBenchmark
    {
        readonly MutableBitField BitField_S = new MutableBitField (5);
        readonly MutableBitField BitField_M = new MutableBitField (50);
        readonly MutableBitField BitField_L = new MutableBitField (500);
        readonly MutableBitField BitField_XL = new MutableBitField (5000);
        readonly MutableBitField BitField_XXL = new MutableBitField (50000);

        readonly MutableBitField Temp_S = new MutableBitField (5);
        readonly MutableBitField Temp_M = new MutableBitField (50);
        readonly MutableBitField Temp_L = new MutableBitField (500);
        readonly MutableBitField Temp_XL = new MutableBitField (5000);
        readonly MutableBitField Temp_XXL = new MutableBitField (50000);

        MutableBitField Selector_S = new MutableBitField (5).SetAll (true);
        MutableBitField Selector_M = new MutableBitField (50).SetAll (true);
        MutableBitField Selector_L = new MutableBitField (500).SetAll (true);
        MutableBitField Selector_XL = new MutableBitField (5000).SetAll (true);
        MutableBitField Selector_XXL = new MutableBitField (50000).SetAll (true);

        public BitfieldBenchmark ()
        {
            BitField_S[1] = true;

            foreach (var bf in new[] { BitField_M, BitField_L, BitField_XL, BitField_XXL })
                for (int i = 31; i < bf.Length; i += 32)
                    bf[i] = true;
        }

        [Benchmark]
        public void FirstTrue ()
            => BitField_L.FirstTrue ();

        [Benchmark]
        public void PopCount ()
            => BitField_L.CountTrue (Selector_L);

        [Benchmark]
        public void NAnd ()
            => Temp_L.From (BitField_L).NAnd (Selector_L);

        [Benchmark]
        public void From_S ()
            => Temp_S.From (BitField_S);

        [Benchmark]
        public void From_M ()
            => Temp_M.From (BitField_M);

        [Benchmark]
        public void From_L ()
            => Temp_L.From (BitField_L);

        [Benchmark]
        public void From_XL ()
            => Temp_XL.From (BitField_XL);

        [Benchmark]
        public void From_XXL ()
            => Temp_XXL.From (BitField_XXL);
    }

    [MemoryDiagnoser]
    public class MessageEncoder
    {
        readonly Memory<byte> Buffer;
        readonly RequestMessage Message;

        public MessageEncoder ()
        {
            Message = new RequestMessage (1, 2, 3);
            Buffer = new Memory<byte> (new byte[20]);
            Message.Encode (Buffer.Span);
        }

        [Benchmark]
        public void Encode ()
        {
            Message.Encode (Buffer.Span);
        }

        [Benchmark]
        public void Decode ()
        {
            Message.Decode (Buffer.Span);
        }
    }

    [MemoryDiagnoser]
    public class BEncodingBenchmark
    {
        readonly byte[] Buffer;
        readonly Stream Stream;
        public BEncodingBenchmark ()
        {
            Buffer = File.ReadAllBytes ("c:\\ubuntu-20.10-live-server-amd64.iso.torrent");
            Stream = new MemoryStream (Buffer);
        }

        [Benchmark]
        public void OldDecode ()
        {
            Stream.Position = 0;
            BEncodedDictionary.DecodeTorrent (Stream);
        }

        [Benchmark]
        public void NewDecode () => BEncodedDictionary.DecodeTorrent (Buffer);
    }

    [MemoryDiagnoser]
    public class SequenceEqual
    {
        readonly BEncodedString Value1 = "c:\\ubuntu-20.10-live-server-amd64.iso.torrent";
        readonly BEncodedString Value2 = "c:\\ubuntu-20.10-live-server-amd64.iso.torrent";

        [Benchmark]
        public void OldCompare () => Value1.Equals (Value2);

        [Benchmark]
        public void NewCompare () => Value1.Span.SequenceEqual (Value2.Span);

    }

    [MemoryDiagnoser]
    public class BEncodedNumberBenchmark
    {
        readonly byte[] buffer = new byte[30];
        readonly BEncodedNumber MinValue = long.MinValue;
        readonly BEncodedNumber MaxValue = long.MaxValue;
        readonly BEncodedNumber ZeroValue = 0;
        readonly BEncodedNumber OneValue = 1;

        public BEncodedNumberBenchmark ()
        {
            MaxValue.Encode (buffer);
        }

        [Benchmark]
        public void Smallest () => MinValue.Encode (buffer);

        [Benchmark]
        public void Largest () => MaxValue.Encode (buffer);

        [Benchmark]
        public void Zero () => ZeroValue.Encode (buffer);

        [Benchmark]
        public void One () => OneValue.Encode (buffer);
    }

    [MemoryDiagnoser]
    public class BEncodedNumberLengthInBytesBenchmark
    {
        readonly BEncodedNumber MinValue = long.MinValue;
        readonly BEncodedNumber MaxValue = long.MaxValue;
        readonly BEncodedNumber ZeroValue = 0;
        readonly BEncodedNumber OneValue = 1;

        public BEncodedNumberLengthInBytesBenchmark ()
        {
        }

        [Benchmark]
        public void Smallest () => MinValue.LengthInBytes ();

        [Benchmark]
        public void Largest () => MaxValue.LengthInBytes ();

        [Benchmark]
        public void Zero () => ZeroValue.LengthInBytes ();

        [Benchmark]
        public void One () => OneValue.LengthInBytes ();
    }

    [MemoryDiagnoser]
    public class StandardPickerBenchmark
    {
        class TorrentData : ITorrentManagerInfo
        {
            const int PieceCount = 500;

            IList<ITorrentFile> ITorrentInfo.Files => Files.ToArray<ITorrentFile> ();
            public IList<ITorrentManagerFile> Files { get; }
            public InfoHash InfoHash { get; } = new InfoHash (new byte[20]);
            public InfoHash InfoHashV2 { get; } = new InfoHash (new byte[32]);
            public string Name { get; } = "Name";
            public int PieceLength { get; } = 32768;
            public long Size { get; } = 32768 * PieceCount;

            public IPeer CreatePeer ()
                => new Peer (PieceCount);
        }

        class Peer : IPeer
        {
            public int AmRequestingPiecesCount { get; set; }
            public BitField BitField { get; }
            public bool CanRequestMorePieces { get; } = true;
            public long DownloadSpeed { get; }
            public List<int> IsAllowedFastPieces { get; }
            public bool IsChoking { get; } = false;
            public bool IsSeeder { get; } = true;
            public int MaxPendingRequests { get; } = int.MaxValue;
            public int RepeatedHashFails { get; }
            public List<int> SuggestedPieces { get; } = new List<int> ();
            public bool SupportsFastPeer { get; } = true;
            public int TotalHashFails { get; }
            public bool CanCancelRequests { get; }

            public int PreferredRequestAmount (int pieceLength)
                => 1;

            public Peer (int pieceCount)
                => BitField = new MutableBitField (pieceCount).SetAll (true);
        }

        readonly TorrentData Data;
        readonly StandardPicker Picker;
        readonly IPeer Requester;
        readonly Queue<BlockInfo> Requested;

        public StandardPickerBenchmark ()
        {
            Data = new TorrentData ();
            Picker = new StandardPicker ();
            Requester = Data.CreatePeer ();
            Requested = new Queue<BlockInfo> ((int)(Data.PieceCount () * Data.BlocksPerPiece (0)));


            Random = new Random (1234);
            Requesters = new List<IPeer> (Enumerable.Range (0, 60).Select (t => Data.CreatePeer ()));
            RequestedBlocks = new List<Queue<BlockInfo>> ();
            foreach (var requester in Requesters)
                RequestedBlocks.Add (new Queue<BlockInfo> (1400));
        }

        [Benchmark]
        public void PickAndValidate ()
        {
            Picker.Initialise (Data);

            BlockInfo? requested;
            while ((requested = Picker.PickPiece (Requester, Requester.BitField)).HasValue) {
                Requested.Enqueue (requested.Value);
            }

            while (Requested.Count > 0)
                Picker.ValidatePiece (Requester, Requested.Dequeue (), out bool _, out _);

        }

        [Benchmark]
        public void PickAndValidate_600Concurrent ()
        {
            Picker.Initialise (new TorrentData ());

            var bf = new MutableBitField (Requester.BitField);
            BlockInfo? requested;
            while ((requested = Picker.PickPiece (Requester, bf)).HasValue) {
                Requested.Enqueue (requested.Value);
                if (Requested.Count > 600) {
                    var popped = Requested.Dequeue ();
                    if (Picker.ValidatePiece (Requester, popped, out bool done, out _))
                        if (done)
                            bf[popped.PieceIndex] = false;
                }
            }

            while (Requested.Count > 0)
                Picker.ValidatePiece (Requester, Requested.Dequeue (), out bool _, out _);
        }

        readonly Random Random;
        readonly List<IPeer> Requesters;
        readonly List<Queue<BlockInfo>> RequestedBlocks;

        [Benchmark]
        public void PickAndValidate_600Concurrent_60Requesters ()
        {
            Picker.Initialise (new TorrentData ());

            var bf = new MutableBitField (Requester.BitField);
            BlockInfo? requested;
            int requestIndex = Random.Next (0, Requesters.Count);
            while ((requested = Picker.PickPiece (Requesters[requestIndex], bf)).HasValue) {
                RequestedBlocks[requestIndex].Enqueue (requested.Value);
                if (RequestedBlocks[requestIndex].Count > 600) {
                    var popped = RequestedBlocks[requestIndex].Dequeue ();
                    if (Picker.ValidatePiece (Requesters[requestIndex], popped, out bool done, out _))
                        if (done)
                            bf[popped.PieceIndex] = false;
                }
            }

            for (int i = 0; i < Requesters.Count; i++) {
                while (RequestedBlocks[i].Count > 0) {
                    var popped = RequestedBlocks[i].Dequeue ();
                    if (Picker.ValidatePiece (Requesters[i], popped, out bool success, out _))
                        if (success)
                            bf[popped.PieceIndex] = false;
                }
            }
            if (!bf.AllFalse)
                throw new Exception ();
        }
    }

    public class Program
    {
        public static void Main (string[] args)
        {
            var summary = BenchmarkRunner.Run (typeof (StandardPickerBenchmark));
        }
    }
}
