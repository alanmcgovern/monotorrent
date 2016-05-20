using System;
using System.Runtime.Serialization;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// </summary>
    public class ListenerException : TorrentException
    {
        /// <summary>
        /// </summary>
        public ListenerException()
        {
        }


        /// <summary>
        /// </summary>
        /// <param name="message"></param>
        public ListenerException(string message)
            : base(message)
        {
        }


        /// <summary>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public ListenerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }


        /// <summary>
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public ListenerException(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}