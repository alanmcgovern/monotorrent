using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class ProtocolException : Exception
    {
        public ProtocolException()
            :base()
        {
        }


        public ProtocolException(string message)
            : base(message)
        {
        }


        public ProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }


        public ProtocolException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
