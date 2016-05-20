using System;
using System.Text;

namespace MonoTorrent.Common
{
    /// <summary>
    ///     This is the base class for the files available to download from within a .torrent.
    ///     This should be inherited by both Client and Tracker "TorrentFile" classes
    /// </summary>
    public class TorrentFile : IEquatable<TorrentFile>
    {
        #region Private Fields

        private BitField selector;

        #endregion Private Fields

        #region Member Variables

        /// <summary>
        ///     The number of pieces which have been successfully downloaded which are from this file
        /// </summary>
        public BitField BitField { get; }

        public long BytesDownloaded
        {
            get { return (long) (BitField.PercentComplete*Length/100.0); }
        }

        /// <summary>
        ///     The ED2K hash of the file
        /// </summary>
        public byte[] ED2K { get; }

        /// <summary>
        ///     The index of the last piece of this file
        /// </summary>
        public int EndPieceIndex { get; }

        public string FullPath { get; internal set; }

        /// <summary>
        ///     The length of the file in bytes
        /// </summary>
        public long Length { get; }

        /// <summary>
        ///     The MD5 hash of the file
        /// </summary>
        public byte[] MD5 { get; internal set; }

        /// <summary>
        ///     In the case of a single torrent file, this is the name of the file.
        ///     In the case of a multi-file torrent this is the relative path of the file
        ///     (including the filename) from the base directory
        /// </summary>
        public string Path { get; }

        /// <summary>
        ///     The priority of this torrent file
        /// </summary>
        public Priority Priority { get; set; }

        /// <summary>
        ///     The SHA1 hash of the file
        /// </summary>
        public byte[] SHA1 { get; }

        /// <summary>
        ///     The index of the first piece of this file
        /// </summary>
        public int StartPieceIndex { get; }

        #endregion

        #region Constructors

        public TorrentFile(string path, long length)
            : this(path, length, path)
        {
        }

        public TorrentFile(string path, long length, string fullPath)
            : this(path, length, fullPath, 0, 0)
        {
        }

        public TorrentFile(string path, long length, int startIndex, int endIndex)
            : this(path, length, path, startIndex, endIndex)
        {
        }

        public TorrentFile(string path, long length, string fullPath, int startIndex, int endIndex)
            : this(path, length, fullPath, startIndex, endIndex, null, null, null)
        {
        }

        public TorrentFile(string path, long length, string fullPath, int startIndex, int endIndex, byte[] md5,
            byte[] ed2k, byte[] sha1)
        {
            BitField = new BitField(endIndex - startIndex + 1);
            ED2K = ed2k;
            EndPieceIndex = endIndex;
            FullPath = fullPath;
            Length = length;
            MD5 = md5;
            Path = path;
            Priority = Priority.Normal;
            SHA1 = sha1;
            StartPieceIndex = startIndex;
        }

        #endregion

        #region Methods

        public override bool Equals(object obj)
        {
            return Equals(obj as TorrentFile);
        }

        public bool Equals(TorrentFile other)
        {
            return other == null ? false : Path == other.Path && Length == other.Length;
            ;
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        internal BitField GetSelector(int totalPieces)
        {
            if (selector != null)
                return selector;

            selector = new BitField(totalPieces);
            for (var i = StartPieceIndex; i <= EndPieceIndex; i++)
                selector[i] = true;
            return selector;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(32);
            sb.Append("File: ");
            sb.Append(Path);
            sb.Append(" StartIndex: ");
            sb.Append(StartPieceIndex);
            sb.Append(" EndIndex: ");
            sb.Append(EndPieceIndex);
            return sb.ToString();
        }

        #endregion Methods
    }
}