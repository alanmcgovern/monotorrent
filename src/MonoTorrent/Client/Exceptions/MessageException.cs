using System;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class MessageException : TorrentException
    {
        public MessageException()
            : base()
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


        public MessageException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
