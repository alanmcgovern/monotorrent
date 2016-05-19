using System;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client.Encryption
{
    public interface IEncryptor
    {
        byte[] InitialData { get; }

        IEncryption Encryptor { get; }
        IEncryption Decryptor { get; }
        void AddPayload(byte[] buffer);
        void AddPayload(byte[] buffer, int offset, int count);

        IAsyncResult BeginHandshake(IConnection socket, AsyncCallback callback, object state);

        IAsyncResult BeginHandshake(IConnection socket, byte[] initialBuffer, int offset, int count,
            AsyncCallback callback, object state);

        void EndHandshake(IAsyncResult result);
    }
}