using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class BitfieldBenchmark_FirstTrueOrFalse
    {
        [Params (true, false)]
        public bool FirstTrue { get; set; }

        [Params(50_000)]
        public int BitFieldSize { get; set; } = 50_000;

        readonly ReadOnlyBitField True_XXL_00000BitField;
        readonly ReadOnlyBitField True_XXL_00063BitField;
        readonly ReadOnlyBitField True_XXL_33333BitField;
        readonly ReadOnlyBitField True_XXL_49999BitField;
        readonly ReadOnlyBitField True_XXL_EverySeventiethTrueBitField;

        readonly ReadOnlyBitField False_XXL_00000BitField;
        readonly ReadOnlyBitField False_XXL_00063BitField;
        readonly ReadOnlyBitField False_XXL_33333BitField;
        readonly ReadOnlyBitField False_XXL_49999BitField;
        readonly ReadOnlyBitField False_XXL_EverySeventiethFalseBitField;

        public BitfieldBenchmark_FirstTrueOrFalse ()
        {
            True_XXL_00000BitField = new BitField (BitFieldSize).Set (0, true);
            True_XXL_00063BitField = new BitField (BitFieldSize).Set (63, true);
            True_XXL_33333BitField = new BitField (BitFieldSize).Set (33333, true);
            True_XXL_49999BitField = new BitField (BitFieldSize).Set (49999, true);

            var bf = new BitField (BitFieldSize).SetAll (false);
            for (int i = 0; i < bf.Length; i += 70)
                bf.Set (i, true);
            True_XXL_EverySeventiethTrueBitField = bf;


            False_XXL_00000BitField = new BitField (True_XXL_00000BitField).Not ();
            False_XXL_00063BitField = new BitField (True_XXL_00063BitField).Not ();
            False_XXL_33333BitField = new BitField (True_XXL_33333BitField).Not ();
            False_XXL_49999BitField = new BitField (True_XXL_49999BitField).Not ();

            bf = new BitField (BitFieldSize).SetAll (true);
            for (int i = 0; i < bf.Length; i += 70)
                bf.Set (i, false);
            False_XXL_EverySeventiethFalseBitField = bf;
        }

        [Benchmark]
        public void True_00000 ()
            => _ = FirstTrue ? True_XXL_00000BitField.FirstTrue () : True_XXL_00000BitField.FirstFalse ();

        [Benchmark]
        public void True_00063 ()
            => _ = FirstTrue ? True_XXL_00063BitField.FirstTrue () : True_XXL_00063BitField.FirstFalse ();

        [Benchmark]
        public void True_33333 ()
            => _ = FirstTrue ? True_XXL_33333BitField.FirstTrue () : True_XXL_33333BitField.FirstFalse ();

        [Benchmark]
        public void True_49999 ()
            => _ = FirstTrue ? True_XXL_49999BitField.FirstTrue () : True_XXL_49999BitField.FirstFalse ();

        [Benchmark]
        public void EverySeventiethTrue ()
        {
            // Find every index
            int next = -1;
            do {
                next++;
                if (FirstTrue)
                    next = True_XXL_EverySeventiethTrueBitField.FirstTrue (next, True_XXL_EverySeventiethTrueBitField.Length - 1);
                else
                    next = True_XXL_EverySeventiethTrueBitField.FirstFalse (next, True_XXL_EverySeventiethTrueBitField.Length - 1);
            } while (next != -1 && next < True_XXL_EverySeventiethTrueBitField.Length - 1);
        }

        [Benchmark]
        public void False_00000 ()
            => _ = FirstTrue ? False_XXL_00000BitField.FirstTrue () : False_XXL_00000BitField.FirstFalse ();

        [Benchmark]
        public void False_00063 ()
            => _ = FirstTrue ? False_XXL_00063BitField.FirstTrue () : False_XXL_00063BitField.FirstFalse ();

        [Benchmark]
        public void False_33333 ()
            => _ = FirstTrue ? False_XXL_33333BitField.FirstTrue () : False_XXL_33333BitField.FirstFalse ();

        [Benchmark]
        public void False_49999 ()
            => _ = FirstTrue ? False_XXL_49999BitField.FirstTrue () : False_XXL_49999BitField.FirstFalse ();


        [Benchmark]
        public void EverySeventiethFalse ()
        {
            // Find every index
            int next = -1;
            do {
                next++;
                if (FirstTrue)
                    next = False_XXL_EverySeventiethFalseBitField.FirstTrue (next, False_XXL_EverySeventiethFalseBitField.Length - 1);
                else
                    next = False_XXL_EverySeventiethFalseBitField.FirstFalse (next, False_XXL_EverySeventiethFalseBitField.Length - 1);
            } while (next != -1 && next < False_XXL_EverySeventiethFalseBitField.Length - 1);
        }
    }

    [MemoryDiagnoser]
    public class BitfieldBenchmark
    {
        readonly ReadOnlyBitField BitField_S = new BitField (5);
        readonly ReadOnlyBitField BitField_M = new BitField (50);
        readonly ReadOnlyBitField BitField_L = new BitField (500);
        readonly ReadOnlyBitField BitField_XL = new BitField (5000);
        readonly ReadOnlyBitField BitField_XXL = new BitField (50000);

        readonly BitField Temp_S = new BitField (5);
        readonly BitField Temp_M = new BitField (50);
        readonly BitField Temp_L = new BitField (500);
        readonly BitField Temp_XL = new BitField (5000);
        readonly BitField Temp_XXL = new BitField (50000);

        readonly ReadOnlyBitField Selector_S = new BitField (5).SetAll (true);
        readonly ReadOnlyBitField Selector_M = new BitField (50).SetAll (true);
        readonly ReadOnlyBitField Selector_L = new BitField (500).SetAll (true);
        readonly ReadOnlyBitField Selector_XL = new BitField (5000).SetAll (true);
        readonly ReadOnlyBitField Selector_XXL = new BitField (50000).SetAll (true);

        readonly ReadOnlyBitField PartialSelector_S = new BitField (5).SetAll (true);
        readonly ReadOnlyBitField PartialSelector_M = new BitField (50).SetAll (true);
        readonly ReadOnlyBitField PartialSelector_L = new BitField (500).SetAll (true);
        readonly ReadOnlyBitField PartialSelector_XL = new BitField (5000).SetAll (true);
        readonly ReadOnlyBitField PartialSelector_XXL = new BitField (50000).SetAll (true);

        public BitfieldBenchmark ()
        {
            BitField_S = new BitField (BitField_S).Set (1, true);

            static ReadOnlyBitField SetSomeTrue (ReadOnlyBitField bitfield)
            {
                var bf = new BitField (bitfield);
                for (int i = 31; i < bf.Length; i += 32)
                    bf[i] = true;
                return bf;
            }
            BitField_M = SetSomeTrue (BitField_M);
            BitField_L = SetSomeTrue (BitField_L);
            BitField_XL = SetSomeTrue (BitField_XL);
            BitField_XXL = SetSomeTrue (BitField_XXL);

            static ReadOnlyBitField SetSomeFalse (ReadOnlyBitField bitfield)
            {
                var bf = new BitField (bitfield);
                for (int i = 31; i < bf.Length; i += 32) {
                    bf[i - 10] = false;
                    bf[i] = false;
                }
                return bf;
            }
            PartialSelector_S = SetSomeFalse (new BitField (PartialSelector_S).SetAll (true));
            PartialSelector_M = SetSomeFalse (new BitField (PartialSelector_M).SetAll (true));
            PartialSelector_L = SetSomeFalse (new BitField (PartialSelector_L).SetAll (true));
            PartialSelector_XL = SetSomeFalse (new BitField (PartialSelector_XL).SetAll (true));
            PartialSelector_XXL = SetSomeFalse (new BitField (PartialSelector_XXL).SetAll (true));
        }

        [Benchmark]
        public void From_S ()
            => Temp_S.From (BitField_S);

        [Benchmark]
        public void From_M ()
            => Temp_M.From (BitField_M);

        [Benchmark]
        public void NAnd_L ()
            => Temp_L.From (BitField_L).NAnd (Selector_L);

        [Benchmark]
        public void NAnd_XL ()
            => Temp_XL.From (BitField_XL).NAnd (PartialSelector_XL);

        [Benchmark]
        public void PopCount ()
            => BitField_L.CountTrue (Selector_L);
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
        class TorrentInfo : ITorrentInfo
        {
            const int PieceCount = 500;

            IList<ITorrentFile> ITorrentInfo.Files => Array.Empty<ITorrentFile> ();
            public int PieceLength { get; } = 32768;
            public long Size { get; } = 32768 * PieceCount;
            public InfoHashes InfoHashes { get; } = new InfoHashes (new InfoHash (new byte[20]), new InfoHash (new byte[32]));
            public string Name => "Name";
        }

        class TorrentData : IPieceRequesterData
        {
            const int PieceCount = 500;

            public InfoHashes InfoHashes { get; } = new InfoHashes (new InfoHash (new byte[20]), new InfoHash (new byte[32]));
            public IList<ITorrentManagerFile> Files { get; } = Array.Empty<ITorrentManagerFile> ();
            public string Name { get; } = "Name";

            public ITorrentInfo TorrentInfo => new TorrentInfo ();

            int IPieceRequesterData.PieceCount => TorrentInfo.PieceCount ();
            int IPieceRequesterData.PieceLength => TorrentInfo.PieceLength;

            public Peer CreatePeer ()
                => new Peer (PieceCount);

            int IPieceRequesterData.SegmentsPerPiece (int piece)
                => TorrentInfo.BlocksPerPiece (piece);

            int IPieceRequesterData.ByteOffsetToPieceIndex (long byteOffset)
                => TorrentInfo.ByteOffsetToPieceIndex (byteOffset);

            int IPieceRequesterData.BytesPerPiece (int piece)
                => TorrentInfo.BytesPerPiece (piece);

            public void EnqueueRequest (IRequester peer, PieceSegment block)
            {
                
            }

            public void EnqueueRequests (IRequester peer, Span<PieceSegment> blocks)
            {
                
            }

            public void EnqueueCancellation (IRequester peer, PieceSegment segment)
            {

            }

            public void EnqueueCancellations (IRequester peer, Span<PieceSegment> segments)
            {

            }
        }

        class Peer : IRequester
        {
            public int AmRequestingPiecesCount { get; set; }
            public ReadOnlyBitField BitField { get; }
            public bool CanRequestMorePieces { get; } = true;
            public long DownloadSpeed { get; }
            public List<int> IsAllowedFastPieces { get; } = new List<int> ();
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
                => BitField = new BitField (pieceCount).SetAll (true);
        }

        readonly TorrentData Data;
        readonly StandardPicker Picker;
        readonly Peer Requester;
        readonly Queue<PieceSegment> Requested;

        public StandardPickerBenchmark ()
        {
            Data = new TorrentData ();
            Picker = new StandardPicker ();
            Requester = Data.CreatePeer ();
            Requested = new Queue<PieceSegment> ((int)(Data.TorrentInfo.PieceCount () * Data.TorrentInfo.BlocksPerPiece (0)));


            Random = new Random (1234);
            Requesters = new List<IRequester> (Enumerable.Range (0, 60).Select (t => Data.CreatePeer ()));
            RequestedBlocks = new List<Queue<PieceSegment>> ();
            foreach (var requester in Requesters)
                RequestedBlocks.Add (new Queue<PieceSegment> (1400));
        }

        [Benchmark]
        public void PickAndValidate ()
        {
            var requesters = new HashSet<IRequester> ();
            Picker.Initialise (Data);

            Span<PieceSegment> requested = stackalloc PieceSegment[1];
            while ((Picker.PickPiece (Requester, Requester.BitField, ReadOnlySpan<ReadOnlyBitField>.Empty, 0, Requester.BitField.Length - 1, requested)) == 1) {
                Requested.Enqueue (requested[0]);
            }

            while (Requested.Count > 0) {
                Picker.ValidatePiece (Requester, Requested.Dequeue (), out bool _, requesters);
                requesters.Clear ();
            }
        }

        [Benchmark]
        public void PickAndValidate_600Concurrent ()
        {
            Picker.Initialise (new TorrentData ());

            var requesters = new HashSet<IRequester> ();
            var bf = new BitField (Requester.BitField);
            Span<PieceSegment> requested = stackalloc PieceSegment[1];
            while ((Picker.PickPiece (Requester, bf, ReadOnlySpan<ReadOnlyBitField>.Empty, 0, bf.Length - 1, requested)) == 1) {
                Requested.Enqueue (requested[0]);
                if (Requested.Count > 600) {
                    var popped = Requested.Dequeue ();
                    if (Picker.ValidatePiece (Requester, popped, out bool pieceComplete, requesters) && pieceComplete) {
                        bf[popped.PieceIndex] = false;
                        requesters.Clear ();
                    }
                }
            }

            while (Requested.Count > 0)
                if (Picker.ValidatePiece (Requester, Requested.Dequeue (), out bool _, requesters))
                    requesters.Clear ();
        }

        readonly Random Random;
        readonly List<IRequester> Requesters;
        readonly List<Queue<PieceSegment>> RequestedBlocks;

        [Benchmark]
        public void PickAndValidate_600Concurrent_60Requesters ()
        {
            Picker.Initialise (new TorrentData ());

            var requesters = new HashSet<IRequester> ();
            var bf = new BitField (Requester.BitField);
            Span<PieceSegment> requested = stackalloc PieceSegment[1];
            int requestIndex = Random.Next (0, Requesters.Count);
            while ((Picker.PickPiece (Requesters[requestIndex], bf, ReadOnlySpan<ReadOnlyBitField>.Empty, 0, bf.Length - 1, requested)) == 1) {
                RequestedBlocks[requestIndex].Enqueue (requested[0]);
                if (RequestedBlocks[requestIndex].Count > 600) {
                    var popped = RequestedBlocks[requestIndex].Dequeue ();
                    if (Picker.ValidatePiece (Requesters[requestIndex], popped, out bool pieceComplete, requesters) && pieceComplete) {
                        bf[popped.PieceIndex] = false;
                        requesters.Clear ();
                    }
                }
            }

            for (int i = 0; i < Requesters.Count; i++) {
                while (RequestedBlocks[i].Count > 0) {
                    var popped = RequestedBlocks[i].Dequeue ();
                    if (Picker.ValidatePiece (Requesters[i], popped, out bool pieceComplete, requesters) && pieceComplete) {
                        bf[popped.PieceIndex] = false;
                        requesters.Clear ();
                    }
                }
            }
            if (!bf.AllFalse)
                throw new Exception ();
        }
    }

    [MemoryDiagnoser]
    public class RC4Benchmark
    {
        static byte[] Key = new byte[32];
        static byte[] Data = new byte[16 * 1024];

        static RC4Benchmark ()
        {
            RandomNumberGenerator.Create ().GetBytes (Key);
            RandomNumberGenerator.Create ().GetBytes (Data);
        }

        [Benchmark]
        public void Encrypt16kB ()
        {
            var rc4 = new MonoTorrent.Connections.Peer.Encryption.RC4 (Key);
            for (int i = 0; i < 1000; i++)
                rc4.Encrypt (Data);
        }
    }

    public class Program
    {
        public static void Main (string[] args)
        {
            BenchmarkDotNet.Configs.IConfig config = null!;
#if DEBUG
            config = new BenchmarkDotNet.Configs.DebugInProcessConfig ();
#endif
            var summary = BenchmarkRunner.Run (typeof (BitfieldBenchmark_FirstTrueOrFalse), config);
        }
    }
}
