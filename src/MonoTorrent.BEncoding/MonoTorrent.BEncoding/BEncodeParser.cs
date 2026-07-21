using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MonoTorrent.BEncoding
{
    public static class BEncodeReaderExtensions
    {
        /// Reads the next token, asserts it opens a dictionary.
        public static void ExpectDictionaryBegin (ref this BEncodeReader reader)
        {
            if (!reader.Read () || reader.Token != BEncodeToken.DictionaryBegin)
                throw new BEncodingException ("Expected dictionary begin 'd'.");
        }

        /// Advances to the next dictionary key.
        /// Returns false (and sets key = default) when DictionaryEnd is reached.
        /// Throws on unexpected end-of-buffer or a non-string key.
        public static bool TryReadKey (ref this BEncodeReader reader, out ReadOnlySpan<byte> key)
        {
            if (!reader.Read ())
                throw new BEncodingException ("Unexpected end of buffer inside dictionary.");

            if (reader.Token == BEncodeToken.DictionaryEnd) { key = default; return false; }

            if (reader.Token != BEncodeToken.String)
                throw new BEncodingException ($"Expected string key, got {reader.Token}.");

            key = reader.Span;
            return true;
        }

        /// Reads the next value, asserts it is a bencoded string, and returns
        /// the raw slice of the string, excluding the length prefix, pointing into <paramref name="raw"/>.
        public static ReadOnlyMemory<byte> CaptureString (ref this BEncodeReader reader, ReadOnlyMemory<byte> raw)
        {
            int start = reader.Position;
            if (!reader.Read () || reader.Token != BEncodeToken.String)
                throw new BEncodingException ("Expected bencoded string.");
            return raw.Slice (reader.Position - reader.Span.Length, reader.Span.Length);
        }

        /// Reads the next value, asserts it is a bencoded integer, and returns
        /// the raw slice (i…e) pointing into <paramref name="raw"/>.
        public static ReadOnlyMemory<byte> CaptureInteger (ref this BEncodeReader reader, ReadOnlyMemory<byte> raw)
        {
            int start = reader.Position;
            if (!reader.Read () || reader.Token != BEncodeToken.Integer)
                throw new BEncodingException ("Expected bencoded integer.");
            return raw.Slice (start, reader.Position - start);
        }

        /// Skips and captures the next complete value (any type, including
        /// nested containers) as a raw slice pointing into <paramref name="raw"/>.
        /// Useful for optional or heterogeneous fields like "v".
        public static ReadOnlyMemory<byte> CaptureAny (ref this BEncodeReader reader, ReadOnlyMemory<byte> raw)
        {
            int start = reader.Position;
            reader.SkipValue ();                           // handles scalars and containers
            return raw.Slice (start, reader.Position - start);
        }
    }
    public static class BEncodeSliceExtensions
    {
        /// Decodes the payload of a captured bencoded string as a span.
        /// The span aliases the original buffer — no allocation.
        public static ReadOnlySpan<byte> DecodeStringSpan (this Memory<byte> slice)
        {
            var r = new BEncodeReader (slice.Span);
            if (!r.Read () || r.Token != BEncodeToken.String)
                throw new BEncodingException ("Slice is not a bencoded string.");
            return r.Span;
        }

        /// Decodes the payload of a captured bencoded string as a Memory<byte>
        /// that still points into the original buffer — no allocation.
        public static ReadOnlyMemory<byte> DecodeStringMemory (this Memory<byte> slice)
        {
            var r = new BEncodeReader (slice.Span);
            if (!r.Read () || r.Token != BEncodeToken.String)
                throw new BEncodingException ("Slice is not a bencoded string.");
            // Position sits after the last payload byte; Span.Length is the payload length.
            // Their difference gives the offset of the payload within the slice.
            int payloadOffset = r.Position - r.Span.Length;
            return slice.Slice (payloadOffset, r.Span.Length);
        }

        /// Decodes the value of a captured bencoded integer.
        public static long DecodeInteger (this Memory<byte> slice)
        {
            var r = new BEncodeReader (slice.Span);
            if (!r.Read () || r.Token != BEncodeToken.Integer)
                throw new BEncodingException ("Slice is not a bencoded integer.");
            return r.Integer;
        }
    }

    public enum BEncodeToken
    {
        None,
        Integer,
        String,
        ListBegin,
        ListEnd,
        DictionaryBegin,
        DictionaryEnd,
    }

    ref struct ContainerStack
    {
        const int MaxDepth = 256;

        ulong _bits0, _bits1, _bits2, _bits3;
        int _count;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Push (bool isDict)
        {
            if (_count >= MaxDepth)
                throw new BEncodingException (
                    $"Nesting depth exceeded {MaxDepth} levels.");

            int word = _count >> 6;
            ulong mask = 1UL << (_count & 63);

            if (isDict)
                Set (word, mask);
            else
                Clear (word, mask);

            _count++;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool PopIsDict ()
        {
            if (_count == 0)
                throw new BEncodingException (
                    "Encountered 'e' with no open container.");

            _count--;
            return Test (_count >> 6, 1UL << (_count & 63));
        }

        public bool IsEmpty => _count == 0;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        void Set (int word, ulong mask)
        {
            switch (word) {
                case 0:
                    _bits0 |= mask;
                    return;
                case 1:
                    _bits1 |= mask;
                    return;
                case 2:
                    _bits2 |= mask;
                    return;
                default:
                    _bits3 |= mask;
                    return;
            }
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        void Clear (int word, ulong mask)
        {
            switch (word) {
                case 0:
                    _bits0 &= ~mask;
                    return;
                case 1:
                    _bits1 &= ~mask;
                    return;
                case 2:
                    _bits2 &= ~mask;
                    return;
                default:
                    _bits3 &= ~mask;
                    return;
            }
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        bool Test (int word, ulong mask) => word switch {
            0 => (_bits0 & mask) != 0,
            1 => (_bits1 & mask) != 0,
            2 => (_bits2 & mask) != 0,
            _ => (_bits3 & mask) != 0,
        };
    }

    public ref struct BEncodeReader
    {
        ReadOnlySpan<byte> _originalBuffer;
        ReadOnlySpan<byte> _buffer;
        ContainerStack _stack;

        /// <summary>The type of the most recently read token.</summary>
        public BEncodeToken Token { get; private set; }

        /// <summary>Valid when <see cref="Token"/> is <see cref="BEncodeToken.Integer"/>.</summary>
        public long Integer { get; private set; }

        public int Position => _originalBuffer.Length - _buffer.Length;

        /// <summary>
        /// Valid when <see cref="Token"/> is <see cref="BEncodeToken.String"/>.
        /// This is a direct slice of the original buffer; it is not null-terminated.
        /// </summary>
        public ReadOnlySpan<byte> Span { get; private set; }

        public BEncodeReader (ReadOnlySpan<byte> buffer)
        {
            _originalBuffer = _buffer = buffer;
            Token = BEncodeToken.None;
            Integer = 0;
            Span = default;
            _stack = default;
        }

        /// <summary>
        /// Reads the next token.  Returns <c>false</c> when the buffer is
        /// exhausted (all open containers have been closed).
        /// </summary>
        public bool Read ()
        {
            Span = default;
            Integer = 0;

            if (_buffer.IsEmpty)
                return false;

            byte first = _buffer[0];

            if (first == 'i') {
                _buffer = _buffer.Slice (1);
                Integer = DecodeNumber (ref _buffer);
                Token = BEncodeToken.Integer;
                return true;
            }

            if (first == 'd') {
                _buffer = _buffer.Slice (1);
                _stack.Push (isDict: true);
                Token = BEncodeToken.DictionaryBegin;
                return true;
            }

            if (first == 'l') {
                _buffer = _buffer.Slice (1);
                _stack.Push (isDict: false);
                Token = BEncodeToken.ListBegin;
                return true;
            }

            if (first == 'e') {
                _buffer = _buffer.Slice (1);
                Token = _stack.PopIsDict ()
                    ? BEncodeToken.DictionaryEnd
                    : BEncodeToken.ListEnd;
                return true;
            }

            if (first >= '0' && first <= '9') {
                int length = 0;
                for (int i = 0; i < _buffer.Length; i++) {
                    byte b = _buffer[i];
                    if (b == ':') {
                        int dataStart = i + 1;
                        if (_buffer.Length - dataStart < length)
                            throw new BEncodingException (
                                $"String declared {length} bytes but only {_buffer.Length - dataStart} remain.");
                        Span = _buffer.Slice (dataStart, length);
                        _buffer = _buffer.Slice (dataStart + length);
                        Token = BEncodeToken.String;
                        return true;
                    }
                    if (b < '0' || b > '9')
                        throw new BEncodingException ($"Non-digit byte 0x{b:X2} in string length prefix.");
                    if (length == 0 && i > 0)
                        throw new BEncodingException ("Leading zero in string length prefix.");
                    int next = length * 10 + (b - '0');
                    if (next < length)
                        throw new BEncodingException ("String length overflowed Int32.");
                    length = next;
                }
            }

            throw new BEncodingException ($"Unexpected byte 0x{first:X2}.");
        }

        static long DecodeNumber (ref ReadOnlySpan<byte> buffer)
        {
            int sign = 1;
            if (buffer[0] == '-') {
                sign = -1;
                buffer = buffer.Slice (1);
            }

            long result = 0;
            for (int i = 0; i < buffer.Length; i++) {
                if (buffer[i] == 'e') {
                    if (i == 0)
                        throw new BEncodingException ("BEncodedNumber did not contain any digits between the 'i' and 'e'");
                    buffer = buffer.Slice (i + 1);
                    return result * sign;
                }
                if (buffer[i] < '0' || buffer[i] > '9')
                    throw new BEncodingException ("Invalid number found.");
                if ((sign == -1 || i > 0) && result == 0 && buffer[i] == '0')
                    throw new BEncodingException ("Invalid number found. The invalid number is negative zero or negative leading zero.");

                result = result * 10 + (buffer[i] - '0');
            }

            throw new BEncodingException ("Invalid number found.");
        }

        /// <summary>
        /// Skips the next complete value (including all of its descendants
        /// if it is a list or dictionary).  Call this before reading the
        /// value you want to ignore.
        /// </summary>
        public void SkipValue ()
        {
            if (!Read ())
                return;

            if (Token == BEncodeToken.Integer || Token == BEncodeToken.String)
                return; // scalar — already consumed

            // Container: read until its own End token at depth 0.
            int depth = 1;
            while (depth > 0) {
                if (!Read ())
                    throw new BEncodingException (
                        "Unexpected end of buffer while skipping value.");

                if (Token == BEncodeToken.ListBegin ||
                    Token == BEncodeToken.DictionaryBegin)
                    depth++;
                else if (Token == BEncodeToken.ListEnd ||
                         Token == BEncodeToken.DictionaryEnd)
                    depth--;
            }
        }
    }

    public ref struct BEncodeWriter
    {
        Span<byte> _buffer;
        int _pos;

        public int Written => _pos;

        public BEncodeWriter (Span<byte> buffer)
        {
            _buffer = buffer;
            _pos = 0;
        }

        void Ensure (int count)
        {
            if (_pos + count > _buffer.Length)
                throw new BEncodingException ("Output buffer too small.");
        }

        public void WriteByte (byte b)
        {
            Ensure (1);
            _buffer[_pos++] = b;
        }

        public void WriteRaw (ReadOnlySpan<byte> data)
        {
            Ensure (data.Length);
            data.CopyTo (_buffer.Slice (_pos));
            _pos += data.Length;
        }

        public void WriteRaw (long value)
        {
            if (!value.TryFormat (_buffer.Slice (_pos), out int written))
                throw new BEncodingException ("Output buffer too small.");
            _pos += written;
        }

        public void BeginDict () => WriteByte ((byte) 'd');

        public void BeginList () => WriteByte ((byte) 'l');

        public void End () => WriteByte ((byte) 'e');

        public void WriteString (ReadOnlySpan<byte> value)
        {
            WriteRaw (value.Length);
            WriteByte ((byte) ':');
            WriteRaw (value);
        }

        public void WriteLong (long value)
        {
            WriteByte ((byte) 'i');
            if (!value.TryFormat (_buffer.Slice (_pos), out int written))
                throw new BEncodingException ("Output buffer too small.");
            _pos += written;
            WriteByte ((byte) 'e');
        }
    }

}
