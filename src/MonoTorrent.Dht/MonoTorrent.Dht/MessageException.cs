using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Dht
{
    public class MessageException : Exception
    {
        private eErrorCode errorCode;

        public eErrorCode ErrorCode
        {
            get { return errorCode; }
        }

        public MessageException(eErrorCode errorCode, string message) : base(message)
        {
            this.errorCode = errorCode;
        }
    }
}
