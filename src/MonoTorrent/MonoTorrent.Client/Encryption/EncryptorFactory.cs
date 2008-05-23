//
// EncryptorFactory.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;
using System.Threading;

namespace MonoTorrent.Client.Encryption
{
    internal class EncryptorAsyncResult : AsyncResult
    {
        public ArraySegment<byte> Buffer;
        public EncryptedSocket EncSocket;
        private PeerIdInternal id;
        private IEncryption decrytor;
        private IEncryption encryptor;


        internal PeerIdInternal Id
        {
            get { return id; }
        }
        public IEncryption Decryptor
        {
            get { return decrytor; }
            set { decrytor = value; }
        }
        public IEncryption Encryptor
        {
            get { return encryptor; }
            set { decrytor = value; }
        }


        public EncryptorAsyncResult(PeerIdInternal id, AsyncCallback callback, object state)
            : base(callback, state)
        {
            this.id = id;
            decrytor = new PlainTextEncryption();
            encryptor = new PlainTextEncryption();
            Buffer = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref Buffer, 16 * 1024);
        }
    }

    internal static class EncryptorFactory
    {
        private static AsyncCallback CompletedPeerACallback = CompletedEncryptedHandshake;

        private static bool CheckRC4(PeerIdInternal id)
        {
            bool canUseRC4 = ClientEngine.SupportsEncryption;

            EncryptionTypes t = id.TorrentManager.Engine.Settings.MinEncryptionLevel;
            canUseRC4 = Toolbox.HasEncryption(t, EncryptionTypes.RC4Header) || Toolbox.HasEncryption(t, EncryptionTypes.RC4Full);

            t = id.Peer.Encryption;
            canUseRC4 = canUseRC4 && Toolbox.HasEncryption(t, EncryptionTypes.RC4Full) || Toolbox.HasEncryption(t, EncryptionTypes.RC4Header);

            return canUseRC4;
        }

        internal static IAsyncResult BeginCheckEncryption(PeerIdInternal id, AsyncCallback callback, object state)
        {
            EncryptorAsyncResult result = new EncryptorAsyncResult(id, callback, state);

            bool canUseRC4 = CheckRC4(id);
            if (canUseRC4)
            {
                if (id.Connection.Connection.IsIncoming)
                {
                    List<byte[]> skeys = new List<byte[]>();
                    id.TorrentManager.Engine.Torrents.ForEach(delegate(TorrentManager m) { skeys.Add(m.Torrent.infoHash); });
                    result.EncSocket = new PeerBEncryption(skeys.ToArray(), EncryptionTypes.Auto);
                }
                else
                {
                    result.EncSocket = new PeerAEncryption(id.TorrentManager.Torrent.infoHash, EncryptionTypes.Auto);
                }

                result.EncSocket.BeginHandshake(id.Connection.Connection, CompletedPeerACallback, result);
            }
            else
            {
                result.Encryptor = new PlainTextEncryption();
                result.Decryptor = new PlainTextEncryption();
                result.CompletedSynchronously = true;
                result.AsyncWaitHandle.Set();
                callback(result);
            }

            return result;
        }

        private static void CompletedEncryptedHandshake(IAsyncResult result)
        {
            EncryptorAsyncResult r = (EncryptorAsyncResult)result.AsyncState;
            try
            {
                r.EncSocket.EndHandshake(result);
            }
            catch (Exception ex)
            {
                r.SavedException = ex;
            }

            r.Decryptor = r.EncSocket.Decryptor;
            r.Encryptor = r.EncSocket.Encryptor;

            r.CompletedSynchronously = false;
            r.AsyncWaitHandle.Set();
            if (r.Callback != null)
                r.Callback(r);
        }

        internal static void EndCheckEncryption(IAsyncResult result)
        {
            EncryptorAsyncResult r = result as EncryptorAsyncResult;
            if (r == null)
                throw new ArgumentException("Invalid async result");

            ClientEngine.BufferManager.FreeBuffer(ref r.Buffer);
            
            if (r.SavedException != null)
                throw r.SavedException;

            r.Id.Connection.Encryptor = r.Encryptor;
            r.Id.Connection.Decryptor = r.Decryptor;
        }
    }
}
