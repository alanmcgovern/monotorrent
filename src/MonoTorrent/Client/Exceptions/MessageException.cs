using System;
using System.Runtime.Serialization;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class MessageException : TorrentException
    {
        public MessageException()
        {
        }


        public MessageException(string message)
            : base(message)
        {
        }


        public MessageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }


        public MessageException(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}