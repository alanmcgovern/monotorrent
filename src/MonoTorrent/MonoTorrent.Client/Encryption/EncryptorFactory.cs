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

        private EncryptedSocket encryptor;
        private PeerIdInternal id;

        public EncryptedSocket Encryptor
        {
            get { return encryptor; }
            set { encryptor = value; }
        }

        public PeerIdInternal Id
        {
            get { return id; }
        }

        public EncryptorAsyncResult(PeerIdInternal id, AsyncCallback callback, object state)
            : base(callback, state)
        {
            this.id = id;
        }


    }

    internal static class EncryptorFactory
    {
        private static AsyncCallback CompletedPeerACallback = CompletedPeerA;

        internal static IAsyncResult BeginCheckEncryption(PeerIdInternal id, AsyncCallback callback, object state)
        {
            EncryptorAsyncResult result = new EncryptorAsyncResult(id, callback, state);

            // Lets see if we should be using RC4
            EncryptionTypes t;
            bool canUseRC4 = ClientEngine.SupportsEncryption;

            t = id.TorrentManager.Engine.Settings.MinEncryptionLevel;
            canUseRC4 = Toolbox.HasEncryption(t, EncryptionTypes.RC4Header) || Toolbox.HasEncryption(t, EncryptionTypes.RC4Full);

            t = id.Peer.Encryption;
            canUseRC4 = canUseRC4 && Toolbox.HasEncryption(t, EncryptionTypes.RC4Full) || Toolbox.HasEncryption(t, EncryptionTypes.RC4Header);

            if (canUseRC4 && !id.Connection.Connection.IsIncoming)
            {
                result.Encryptor = new PeerAEncryption(id.TorrentManager.Torrent.infoHash, EncryptionTypes.Auto);
                result.Encryptor.BeginHandshake(id.Connection.Connection, CompletedPeerACallback, result);
            }
            else
            {
                id.Connection.Encryptor = new PlainTextEncryption();
                id.Connection.Decryptor = new PlainTextEncryption();
                result.CompletedSynchronously = true;
                result.AsyncWaitHandle.Set();
                callback(result);
            }

            return result;
        }

        private static void CompletedPeerA(IAsyncResult result)
        {
            EncryptorAsyncResult r = (EncryptorAsyncResult)result.AsyncState;
            try
            {
                r.Encryptor.EndHandshake(result);
            }
            catch (Exception ex)
            {
                r.SavedException = ex;
            }
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

            if (r.SavedException != null)
                throw r.SavedException;
        }
    }
}
