using System;
using System.Net;

namespace MonoTorrent.Client.Connections
{
    public interface IConnection : IDisposable
    {
        byte[] AddressBytes { get; }

        bool Connected { get; }

        bool CanReconnect { get; }

        bool IsIncoming { get; }

        EndPoint EndPoint { get; }

        Uri Uri { get; }

        IAsyncResult BeginConnect(AsyncCallback callback, object state);
        void EndConnect(IAsyncResult result);

        IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state);
        int EndReceive(IAsyncResult result);

        IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state);
        int EndSend(IAsyncResult result);
    }
}