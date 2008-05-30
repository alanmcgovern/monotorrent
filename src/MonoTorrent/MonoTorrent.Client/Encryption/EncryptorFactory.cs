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
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Encryption
{
    internal class EncryptorAsyncResult : AsyncResult
    {
        public byte[][] SKeys;
        public int Available;
        public byte[] Buffer;
        public byte[] InitialData;
        public IEncryptor EncSocket;
        public PeerIdInternal Id;
        public IEncryption Decryptor;
        public IEncryption Encryptor;


        public EncryptorAsyncResult(PeerIdInternal id, AsyncCallback callback, object state)
            : base(callback, state)
        {
            Id = id;
            Decryptor = new PlainTextEncryption();
            Encryptor = new PlainTextEncryption();
        }
    }

    internal static class EncryptorFactory
    {
        private static readonly AsyncCallback CompletedEncryptedHandshakeCallback = CompletedEncryptedHandshake;
        private static readonly AsyncCallback HandshakeReceivedCallback = HandshakeReceived;

        private static bool CheckRC4(PeerIdInternal id)
        {
            bool canUseRC4 = ClientEngine.SupportsEncryption;

            EncryptionTypes t = EncryptionTypes.Auto;// id.TorrentManager.Engine.Settings.MinEncryptionLevel;
            canUseRC4 = canUseRC4 && (Toolbox.HasEncryption(t, EncryptionTypes.RC4Header) || Toolbox.HasEncryption(t, EncryptionTypes.RC4Full));

            t = id.Peer.Encryption;
            canUseRC4 = canUseRC4 && (Toolbox.HasEncryption(t, EncryptionTypes.RC4Full) || Toolbox.HasEncryption(t, EncryptionTypes.RC4Header));

            return canUseRC4;
        }

        internal static IAsyncResult BeginCheckEncryption(PeerIdInternal id, AsyncCallback callback, object state)
        {
            return BeginCheckEncryption(id, callback, state, null);
        }


        internal static IAsyncResult BeginCheckEncryption(PeerIdInternal id, AsyncCallback callback, object state, byte[][] sKeys)
        {
            EncryptorAsyncResult result = new EncryptorAsyncResult(id, callback, state);
            result.SKeys = sKeys;

            bool supportRC4 = CheckRC4(id);
            IConnection c = id.Connection.Connection;
            try
            {
                // If the connection is incoming, receive the handshake before
                // trying to decide what encryption to use
                if (id.Connection.Connection.IsIncoming)
                {
                    result.Buffer = new byte[id.Connection.BytesToRecieve];
                    c.BeginReceive(result.Buffer, 0, result.Buffer.Length, HandshakeReceivedCallback, result);
                }
                else
                {
                    // If we have an outgoing connection, if RC4 is allowable, negiotiate the encryption method
                    // otherwise just use PlainText
                    if (supportRC4)
                    {
                        result.EncSocket = new PeerAEncryption(id.TorrentManager.Torrent.infoHash, EncryptionTypes.Auto);
                        result.EncSocket.BeginHandshake(id.Connection.Connection, CompletedEncryptedHandshakeCallback, result);
                    }
                    else
                    {
                        result.Complete();
                    }
                }
            }
            catch (Exception ex)
            {
                result.Complete(ex);
            }
            return result;
        }

        internal static void EndCheckEncryption(IAsyncResult result, out byte[] initialData)
        {
            EncryptorAsyncResult r = result as EncryptorAsyncResult;

            if (!r.IsCompleted)
                r.AsyncWaitHandle.WaitOne();

            if (r == null)
                throw new ArgumentException("Invalid async result");

            if (r.SavedException != null)
                throw r.SavedException;

            r.Id.Connection.Encryptor = r.Encryptor;
            r.Id.Connection.Decryptor = r.Decryptor;
            initialData = r.InitialData;
        }

        private static void HandshakeReceived(IAsyncResult r)
        {
            int received = 0;
            EncryptorAsyncResult result = (EncryptorAsyncResult)r.AsyncState;
            IConnection connection = result.Id.Connection.Connection;

            try
            {
                received = connection.EndReceive(r);
                result.Available += received;

                if (received == 0)
                {
                    result.Complete(new EncryptionException("Socket returned zero"));
                    return;
                }
                if (received < result.Buffer.Length)
                {
                    connection.BeginReceive(result.Buffer, result.Available, result.Buffer.Length - result.Available,
                                    HandshakeReceivedCallback, result);
                    return;
                }

                HandshakeMessage message = new HandshakeMessage();
                message.Decode(result.Buffer, 0, result.Buffer.Length);
                bool valid = message.ProtocolString == VersionInfo.ProtocolStringV100;
                bool canUseRC4 = CheckRC4(result.Id);

                // If encryption is disabled and we received an invalid handshake - abort!
                if (valid)
                {
                    result.InitialData = result.Buffer;
                    result.Complete();
                    return;
                }
                if (!canUseRC4 && !valid)
                {
                    result.Complete(new EncryptionException("Invalid handshake received and no decryption works"));
                    return;
                }
                if (canUseRC4)
                {
                    // The data we just received was part of an encrypted handshake and was *not* the BitTorrent handshake
                    result.EncSocket = new PeerBEncryption(result.SKeys, EncryptionTypes.Auto);
                    result.EncSocket.BeginHandshake(connection, result.Buffer, 0, result.Buffer.Length, CompletedEncryptedHandshakeCallback, result);
                }
                else
                {
                    result.Complete();
                }
            }
            catch (Exception ex)
            {
                result.Complete(ex);
                return;
            }
        }

        private static void CompletedEncryptedHandshake(IAsyncResult result)
        {
            EncryptorAsyncResult r = (EncryptorAsyncResult)result.AsyncState;
            try
            {
                r.EncSocket.EndHandshake(result);

                r.Decryptor = r.EncSocket.Decryptor;
                r.Encryptor = r.EncSocket.Encryptor;
                r.InitialData = r.EncSocket.InitialData;
            }
            catch (Exception ex)
            {
                r.SavedException = ex;
            }

            r.Complete();
        }
    }
}
