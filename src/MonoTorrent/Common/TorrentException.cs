using System;
using System.Runtime.Serialization;

namespace MonoTorrent.Common
{
    [Serializable]
    public class TorrentException : Exception
    {
        public TorrentException()
        {
        }

        public TorrentException(string message)
            : base(message)
        {
        }

        public TorrentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public TorrentException(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}