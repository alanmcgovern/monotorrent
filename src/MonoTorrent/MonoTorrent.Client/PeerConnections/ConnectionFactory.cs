using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Connections
{
    public static class ConnectionFactory
    {
        private static object locker = new object();
        private static Dictionary<string, Func<Uri, IConnection>> trackerTypes = new Dictionary<string, Func<Uri, IConnection>>();
       
        static ConnectionFactory()
        {
            RegisterTypeForProtocol("ipv4", uri => new IPV4Connection (uri));
            RegisterTypeForProtocol("ipv6", uri => new IPV6Connection (uri));
            RegisterTypeForProtocol("http", uri => new HttpConnection (uri));
        }

        public static void RegisterTypeForProtocol(string protocol, Type connectionType)
        {
            if (string.IsNullOrEmpty(protocol))
                throw new ArgumentException("cannot be null or empty", "protocol");
            if (connectionType == null)
                throw new ArgumentNullException("connectionType");

            RegisterTypeForProtocol (protocol, uri => (IConnection) Activator.CreateInstance (connectionType, uri));
        }

        static void RegisterTypeForProtocol(string protocol, Func<Uri, IConnection> creator)
        {
            lock (locker)
                trackerTypes[protocol] = creator;
        }


        public static IConnection Create(Uri connectionUri)
        {
            if (connectionUri == null)
                throw new ArgumentNullException("connectionUrl");

            if (connectionUri.Scheme == "ipv4" && connectionUri.Port == -1)
                return null;

            Func<Uri, IConnection> creator;
            lock (locker)
                if (!trackerTypes.TryGetValue(connectionUri.Scheme, out creator))
                    return null;

            try
            {
                return creator (connectionUri);
            }
            catch
            {
                return null;
            }
        }
    }
}
