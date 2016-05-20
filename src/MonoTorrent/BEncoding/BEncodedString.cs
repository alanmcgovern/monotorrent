using System;
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    ///     Class representing a BEncoded string
    /// </summary>
    public class BEncodedString : BEncodedValue, IComparable<BEncodedString>
    {
        #region Member Variables

        /// <summary>
        ///     The value of the BEncodedString
        /// </summary>
        public string Text
        {
            get { return Encoding.UTF8.GetString(TextBytes); }
            set { TextBytes = Encoding.UTF8.GetBytes(value); }
        }

        /// <summary>
        ///     The underlying byte[] associated with this BEncodedString
        /// </summary>
        public byte[] TextBytes { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        ///     Create a new BEncodedString using UTF8 encoding
        /// </summary>
        public BEncodedString()
            : this(new byte[0])
        {
        }

        /// <summary>
        ///     Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value"></param>
        public BEncodedString(char[] value)
            : this(Encoding.UTF8.GetBytes(value))
        {
        }

        /// <summary>
        ///     Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value">Initial value for the string</param>
        public BEncodedString(string value)
            : this(Encoding.UTF8.GetBytes(value))
        {
        }


        /// <summary>
        ///     Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value"></param>
        public BEncodedString(byte[] value)
        {
            TextBytes = value;
        }


        public static implicit operator BEncodedString(string value)
        {
            return new BEncodedString(value);
        }

        public static implicit operator BEncodedString(char[] value)
        {
            return new BEncodedString(value);
        }

        public static implicit operator BEncodedString(byte[] value)
        {
            return new BEncodedString(value);
        }

        #endregion

        #region Encode/Decode Methods

        /// <summary>
        ///     Encodes the BEncodedString to a byte[] using the supplied Encoding
        /// </summary>
        /// <param name="buffer">The buffer to encode the string to</param>
        /// <param name="offset">The offset at which to save the data to</param>
        /// <param name="e">The encoding to use</param>
        /// <returns>The number of bytes encoded</returns>
        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;
            written += Message.WriteAscii(buffer, written, TextBytes.Length.ToString());
            written += Message.WriteAscii(buffer, written, ":");
            written += Message.Write(buffer, written, TextBytes);
            return written - offset;
        }


        /// <summary>
        ///     Decodes a BEncodedString from the supplied StreamReader
        /// </summary>
        /// <param name="reader">The StreamReader containing the BEncodedString</param>
        internal override void DecodeInternal(RawReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            int letterCount;
            var length = string.Empty;

            while ((reader.PeekByte() != -1) && (reader.PeekByte() != ':')) // read in how many characters
                length += (char) reader.ReadByte(); // the string is

            if (reader.ReadByte() != ':') // remove the ':'
                throw new BEncodingException("Invalid data found. Aborting");

            if (!int.TryParse(length, out letterCount))
                throw new BEncodingException(
                    string.Format("Invalid BEncodedString. Length was '{0}' instead of a number", length));

            TextBytes = new byte[letterCount];
            if (reader.Read(TextBytes, 0, letterCount) != letterCount)
                throw new BEncodingException("Couldn't decode string");
        }

        #endregion

        #region Helper Methods

        public string Hex
        {
            get { return BitConverter.ToString(TextBytes); }
        }

        public override int LengthInBytes()
        {
            // The length is equal to the length-prefix + ':' + length of data
            var prefix = 1; // Account for ':'

            // Count the number of characters needed for the length prefix
            for (var i = TextBytes.Length; i != 0; i = i/10)
                prefix += 1;

            if (TextBytes.Length == 0)
                prefix++;

            return prefix + TextBytes.Length;
        }

        public int CompareTo(object other)
        {
            return CompareTo(other as BEncodedString);
        }


        public int CompareTo(BEncodedString other)
        {
            if (other == null)
                return 1;

            var difference = 0;
            var length = TextBytes.Length > other.TextBytes.Length ? other.TextBytes.Length : TextBytes.Length;

            for (var i = 0; i < length; i++)
                if ((difference = TextBytes[i].CompareTo(other.TextBytes[i])) != 0)
                    return difference;

            if (TextBytes.Length == other.TextBytes.Length)
                return 0;

            return TextBytes.Length > other.TextBytes.Length ? 1 : -1;
        }

        #endregion

        #region Overridden Methods

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            BEncodedString other;
            if (obj is string)
                other = new BEncodedString((string) obj);
            else if (obj is BEncodedString)
                other = (BEncodedString) obj;
            else
                return false;

            return Toolbox.ByteMatch(TextBytes, other.TextBytes);
        }

        public override int GetHashCode()
        {
            var hash = 0;
            for (var i = 0; i < TextBytes.Length; i++)
                hash += TextBytes[i];

            return hash;
        }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(TextBytes);
        }

        #endregion
    }
}