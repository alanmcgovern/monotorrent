using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client.PeerConnections
{
    internal class HTTPConnection : PeerConnectionBase
    {
        public HTTPConnection(string location, int bitfieldLength, IEncryptor encryptor)
            : base(bitfieldLength, encryptor)
        {
            throw new NotImplementedException();
        }
        internal override void BeginConnect(AsyncCallback peerEndCreateConnection, PeerConnectionID id)
        {
            throw new NotImplementedException();
        }

        internal override void BeginReceive(byte[] buffer, int offset, int count, System.Net.Sockets.SocketFlags socketFlags, AsyncCallback asyncCallback, PeerConnectionID id)
        {
            throw new NotImplementedException();
        }

        internal override void BeginSend(byte[] buffer, int offset, int count, System.Net.Sockets.SocketFlags socketFlags, AsyncCallback asyncCallback, PeerConnectionID id)
        {
            throw new NotImplementedException();
        }

        internal override void EndConnect(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        internal override int EndReceive(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        internal override int EndSend(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
