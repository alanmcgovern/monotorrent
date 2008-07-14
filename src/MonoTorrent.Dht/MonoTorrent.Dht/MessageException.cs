using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Dht
{
    public class MessageException : Exception
    {
        private eErrorCode errorCode;
        private string message;

        public eErrorCode ErrorCode
        {
            get { return errorCode; }
        }

        public string Message
        {
            get { return message; }
        }

        public MessageException(eErrorCode errorCode, string message)
        {
            this.errorCode = errorCode;
            this.message = message;
        }
    }
}
