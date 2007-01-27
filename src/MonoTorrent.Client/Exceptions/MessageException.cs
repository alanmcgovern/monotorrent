using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class MessageException : MonoTorrentException
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
