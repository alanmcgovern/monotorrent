using System;
using System.Runtime.Serialization;

namespace MonoTorrent.BEncoding
{
    [Serializable]
    public class BEncodingException : Exception
    {
        public BEncodingException()
        {
        }

        public BEncodingException(string message)
            : base(message)
        {
        }

        public BEncodingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected BEncodingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}