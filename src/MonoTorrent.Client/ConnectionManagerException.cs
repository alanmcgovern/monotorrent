using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class ConnectionManagerException : Exception
    {
        /// <summary>
        /// 
        /// </summary>
        public ConnectionManagerException()
            : base()
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public ConnectionManagerException(string message)
            : base(message)
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public ConnectionManagerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public ConnectionManagerException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
