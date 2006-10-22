using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class ListenerException : Exception
    {
        /// <summary>
        /// 
        /// </summary>
        public ListenerException()
            : base()
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public ListenerException(string message)
            : base(message)
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public ListenerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public ListenerException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
