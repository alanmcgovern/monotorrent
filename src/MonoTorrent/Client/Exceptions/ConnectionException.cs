using System;
using System.Runtime.Serialization;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// </summary>
    public class ConnectionException : TorrentException
    {
        /// <summary>
        /// </summary>
        public ConnectionException()
        {
        }


        /// <summary>
        /// </summary>
        /// <param name="message"></param>
        public ConnectionException(string message)
            : base(message)
        {
        }


        /// <summary>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public ConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }


        /// <summary>
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public ConnectionException(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}