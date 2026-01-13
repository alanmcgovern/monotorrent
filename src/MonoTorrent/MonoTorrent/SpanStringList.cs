using System;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    /// <summary>
    /// Represents a list of string segments that can be efficiently concatenated and compared.
    /// </summary>
    public sealed class SpanStringList : IEquatable<SpanStringList>, IEquatable<string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanStringList"/> class with a single segment.
        /// </summary>
        /// <param name="value">The underlying string.</param>
        /// <param name="start">The starting index of the segment.</param>
        /// <param name="length">The length of the segment.</param>
        public SpanStringList (string value, int start, int length) : this (value, start, length, null)
        {
            Value = value;
            Start = start;
            Length = length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanStringList"/> class as a continuation of another list.
        /// </summary>
        /// <param name="value">The underlying string for the new segment.</param>
        /// <param name="start">The starting index of the new segment.</param>
        /// <param name="length">The length of the new segment.</param>
        /// <param name="prev">The previous list of segments.</param>
        public SpanStringList (string value, int start, int length, SpanStringList? prev)
        {
            Value = value;
            Start = start;
            Length = length;
            Prev = prev;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanStringList"/> class from a whole string.
        /// </summary>
        /// <param name="value">The string value.</param>
        public SpanStringList (string value) : this (value, 0, value.Length, null)
        {
        }

        string Value { get; }

        int Start { get; }

        int Length { get; }

        SpanStringList? Prev { get; }

        /// <summary>
        /// Appends a new string segment to the end of the list.
        /// </summary>
        /// <param name="value">The string value to append.</param>
        /// <returns>A new <see cref="SpanStringList"/> containing the appended segment.</returns>
        public SpanStringList Append (string value)
        {
            return Append (value, 0, value.Length);
        }

        /// <summary>
        /// Appends a new string segment to the end of the list.
        /// </summary>
        /// <param name="value">The underlying string for the new segment.</param>
        /// <param name="start">The starting index of the segment.</param>
        /// <param name="length">The length of the segment.</param>
        /// <returns>A new <see cref="SpanStringList"/> containing the appended segment.</returns>
        public SpanStringList Append (string value, int start, int length)
        {
            return new SpanStringList (value, start, length, this);
        }

        /// <summary>
        /// Converts the list of segments into a single string.
        /// </summary>
        /// <returns>A string representation of the combined segments.</returns>
        public override string ToString ()
        {
            SpanStringList? current;
            if (Prev is null) {
                current = this;
                if (current.Start == 0 && current.Length == current.Value.Length)
                    return current.Value;
                return current.Value.AsSpan ().Slice (current.Start, current.Length).ToString ();
            }

            var length = 0;
            current = this;
            do {
                length += current.Length;
                current = current.Prev;
            } while (current is not null);
#if NET5_0_OR_GREATER
            var result = string.Create (length, this, FillSpan);
#else
            var chars = new char[length];
            var span = chars.AsSpan ();
            FillSpan (span, this);
            var result = new string (chars);
#endif
            return result;
        }

        /// <summary>
        /// Fills the provided span with the character data from the segment list.
        /// </summary>
        /// <param name="span">The target span to fill.</param>
        /// <param name="spanList">The list of segments to copy from.</param>
        static void FillSpan (Span<char> span, SpanStringList spanList)
        {
            var offset = span.Length;
            var current = spanList;
            do {
                var toCopy = current.Value.AsSpan ().Slice (current.Start, current.Length);
                var copyTo = span.Slice (offset - toCopy.Length, toCopy.Length);
                toCopy.CopyTo (copyTo);
                offset -= toCopy.Length;
                current = current.Prev;
            } while (current is not null);
        }

        /// <summary>
        /// Implicitly converts a string to a <see cref="SpanStringList"/>.
        /// </summary>
        /// <param name="value">The string value.</param>
        public static implicit operator SpanStringList (string value)
        {
            return new SpanStringList (value);
        }

        /// <summary>
        /// Implicitly converts a <see cref="SpanStringList"/> to a string.
        /// </summary>
        /// <param name="value">The list of segments.</param>
        public static implicit operator string (SpanStringList value)
        {
            return value.ToString ();
        }

        /// <summary>
        /// Explicitly converts a <see cref="SpanStringList"/> to a <see cref="BEncodedString"/>.
        /// </summary>
        /// <param name="value">The list of segments.</param>
        public static explicit operator BEncodedString (SpanStringList value)
        {
            return value.ToString ();
        }

        /// <inheritdoc />
        public override bool Equals (object? obj)
        {
            return ReferenceEquals (this, obj) || obj is SpanStringList other && Equals (other);
        }

        /// <inheritdoc />
        public override int GetHashCode ()
        {
            var result = 397;
            var item = this;
            do {
                var span = item.Value.AsSpan (item.Start, item.Length);
                for (int i = span.Length - 1; i >= 0; i--) {
                    result ^= (span[i].GetHashCode () * 397);
                }

                item = item.Prev;
            } while (item is not null);

            return result;
        }

        /// <summary>
        /// Determines whether two specified <see cref="SpanStringList"/> instances are equal.
        /// </summary>
        /// <param name="left">The first list to compare.</param>
        /// <param name="right">The second list to compare.</param>
        /// <returns>true if the lists are equal; otherwise, false.</returns>
        public static bool operator == (SpanStringList? left, SpanStringList? right)
        {
            return Equals (left, right);
        }

        /// <summary>
        /// Determines whether two specified <see cref="SpanStringList"/> instances are not equal.
        /// </summary>
        /// <param name="left">The first list to compare.</param>
        /// <param name="right">The second list to compare.</param>
        /// <returns>true if the lists are not equal; otherwise, false.</returns>
        public static bool operator != (SpanStringList? left, SpanStringList? right)
        {
            return !Equals (left, right);
        }

        /// <summary>
        /// Determines whether a <see cref="SpanStringList"/> instance and a string are equal.
        /// </summary>
        /// <param name="left">The list to compare.</param>
        /// <param name="right">The string to compare.</param>
        /// <returns>true if they are equal; otherwise, false.</returns>
        public static bool operator == (SpanStringList? left, string? right)
        {
            if (left is null && right is null) return true;
            if (left is null && right is not null) return false;
            return left!.Equals (right);
        }

        /// <summary>
        /// Determines whether a <see cref="SpanStringList"/> instance and a string are not equal.
        /// </summary>
        /// <param name="left">The list to compare.</param>
        /// <param name="right">The string to compare.</param>
        /// <returns>true if they are not equal; otherwise, false.</returns>
        public static bool operator != (SpanStringList? left, string? right)
        {
            if (left is null && right is null)
                return false;
            if (left is null && right is not null)
                return true;
            return !left!.Equals (right);
        }

        /// <summary>
        /// Determines whether a string and a <see cref="SpanStringList"/> instance are equal.
        /// </summary>
        /// <param name="left">The string to compare.</param>
        /// <param name="right">The list to compare.</param>
        /// <returns>true if they are equal; otherwise, false.</returns>
        public static bool operator == (string? left, SpanStringList? right)
        {
            if (right is null && left is null)
                return true;
            if (right is null && left is not null)
                return false;
            return right!.Equals (left);
        }

        /// <summary>
        /// Determines whether a string and a <see cref="SpanStringList"/> instance are not equal.
        /// </summary>
        /// <param name="left">The string to compare.</param>
        /// <param name="right">The list to compare.</param>
        /// <returns>true if they are not equal; otherwise, false.</returns>
        public static bool operator != (string? left, SpanStringList? right)
        {
            if (right is null && left is null)
                return false;
            if (right is null && left is not null)
                return true;
            return !right!.Equals (left);
        }

        /// <summary>
        /// Determines whether the current <see cref="SpanStringList"/> is equal to a specified string.
        /// </summary>
        /// <param name="other">The string to compare with the current list.</param>
        /// <returns>true if the list is equal to the string; otherwise, false.</returns>
        public bool Equals (string? other)
        {
            if (other is null)
                return false;
            if (Prev is null) {
                if (Start == 0 && Length == Value.Length)
                    return other == Value;
                return Value.AsSpan ().Slice (Start, Length).Equals (other, StringComparison.Ordinal);
            }

            var offset = 0;
            var item = this;
            do {
                if (item.Length > other.Length - offset)
                    return false;
                var otherSpan = other.AsSpan ().Slice (other.Length - offset - item.Length, item.Length);
                var thisSpan = item.Value.AsSpan (item.Start, item.Length);
                if (!thisSpan.Equals (otherSpan, StringComparison.Ordinal))
                    return false;
                offset += item.Length;
                item = item.Prev;
            } while (item is not null);

            if (offset != other.Length)
                return false;
            return true;
        }

        /// <summary>
        /// Determines whether the current <see cref="SpanStringList"/> is equal to another <see cref="SpanStringList"/>.
        /// </summary>
        /// <param name="other">The list to compare with the current list.</param>
        /// <returns>true if the lists are equal; otherwise, false.</returns>
        public bool Equals (SpanStringList? other)
        {
            if (other is null)
                return false;
            if (Prev is null && other.Prev is null) {
                if (Start == 0 && Length == Value.Length && other.Start == 0 && other.Length == other.Value.Length)
                    return other.Value == Value;
                return Value.AsSpan ().Slice (Start, Length).Equals (other.Value.AsSpan ().Slice (other.Start, other.Length), StringComparison.Ordinal);
            }

            var thisOffset = 0;
            var otherOffset = 0;
            var thisItem = this;
            var otherItem = other;
            do {
                if (thisItem is null || otherItem is null)
                    return false;
                var thisLength = thisItem.Length - thisOffset;
                var otherLength = otherItem.Length - otherOffset;
                if (thisLength < otherLength) {
                    var thisSpan = thisItem.Value.AsSpan ().Slice (thisItem.Start, thisItem.Length).Slice (0, thisLength);
                    var otherSpan = otherItem.Value.AsSpan ().Slice (otherItem.Start, otherItem.Length).Slice (otherLength - thisLength, thisLength);
                    if (!thisSpan.Equals (otherSpan, StringComparison.Ordinal))
                        return false;
                    otherOffset += thisLength;
                    thisItem = thisItem.Prev;
                    thisOffset = 0;
                } else if (thisLength > otherLength) {
                    var otherSpan = otherItem.Value.AsSpan ().Slice (otherItem.Start, otherItem.Length).Slice (0, otherLength);
                    var thisSpan = thisItem.Value.AsSpan ().Slice (thisItem.Start, thisItem.Length).Slice (thisLength - otherLength, otherLength);
                    if (!otherSpan.Equals (thisSpan, StringComparison.Ordinal))
                        return false;
                    thisOffset += otherLength;
                    otherItem = otherItem.Prev;
                    otherOffset = 0;
                } else {
                    var otherSpan = otherItem.Value.AsSpan ().Slice (otherItem.Start, otherLength);
                    var thisSpan = thisItem.Value.AsSpan ().Slice (thisItem.Start, thisLength);
                    if (!otherSpan.Equals (thisSpan, StringComparison.Ordinal))
                        return false;
                    thisOffset = 0;
                    otherOffset = 0;
                    thisItem = thisItem.Prev;
                    otherItem = otherItem.Prev;
                }
            } while (thisItem is not null || otherItem is not null);

            return true;
        }
    }
}
