using System;
using System.IO;
using System.Security.Cryptography;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Messages.Peer;

namespace MyBenchmarks
{
    [MemoryDiagnoser]
    public class BitfieldBenchmark
    {
        MutableBitField BitField_S = new MutableBitField (5);
        MutableBitField BitField_M = new MutableBitField (50);
        MutableBitField BitField_L = new MutableBitField (500);
        MutableBitField BitField_XL = new MutableBitField (5000);
        MutableBitField BitField_XXL = new MutableBitField (50000);

        MutableBitField Temp_S = new MutableBitField (5);
        MutableBitField Temp_M = new MutableBitField (50);
        MutableBitField Temp_L = new MutableBitField (500);
        MutableBitField Temp_XL = new MutableBitField (5000);
        MutableBitField Temp_XXL = new MutableBitField (50000);

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

    public class Program
    {
        public static void Main (string[] args)
        {
            var summary = BenchmarkRunner.Run (typeof (BitfieldBenchmark));
        }
    }
}
