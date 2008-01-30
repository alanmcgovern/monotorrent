using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Connections
{
    public static class ConnectionFactory
    {
        private static object locker = new object();
        private static Dictionary<string, Type> trackerTypes = new Dictionary<string, Type>();
       
        static ConnectionFactory()
        {
            RegisterTypeForProtocol("tcp", typeof(TCPConnection));
            //RegisterTypeForProtocol("http", typeof(HttpConnection));
        }



        public static void RegisterTypeForProtocol(string protocol, Type connectionType)
        {
            lock (locker)
                trackerTypes.Add(protocol, connectionType);
        }

        public static IConnection Create(Uri connectionUri)
        {
            Type type;
            lock (locker)
                if (!trackerTypes.TryGetValue(connectionUri.Scheme, out type))
                    return null;

            return (IConnection)Activator.CreateInstance(type, connectionUri);
        }
    }
}
