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
    internal static class EncryptorFactory
    {
        private class EncryptorAsyncResult : AsyncResult
        {
            public byte[][] SKeys;
            public int Available;
            public byte[] Buffer;
            public byte[] InitialData;
            public IEncryptor EncSocket;
            public PeerId Id;
            public IEncryption Decryptor;
            public IEncryption Encryptor;


            public EncryptorAsyncResult(PeerId id, AsyncCallback callback, object state)
                : base(callback, state)
            {
                Id = id;
                Decryptor = new PlainTextEncryption();
                Encryptor = new PlainTextEncryption();
            }
        }

        private static readonly AsyncCallback CompletedEncryptedHandshakeCallback = CompletedEncryptedHandshake;
        private static readonly AsyncTransfer HandshakeReceivedCallback = HandshakeReceived;

        private static bool CheckRC4(PeerId id)
        {
            // By default we assume all encryption levels are allowed. This is
            // needed when we receive an incoming connection, because that is not
            // associated with an engine and so we cannot check the engines settings
            EncryptionTypes t = EncryptionTypes.All;

            // If the connection is *not* incoming, then it will be associated with an Engine
            // so we can check what encryption levels the engine allows.
            if (!id.Connection.IsIncoming)
                t = id.TorrentManager.Engine.Settings.AllowedEncryption;

            // We're allowed use encryption if the engine settings allow it and the peer supports it
            // Binary AND both the engine encryption and peer encryption and check what levels are supported
            t = t & id.Peer.Encryption;
            return ClientEngine.SupportsEncryption &&
                   (Toolbox.HasEncryption(t, EncryptionTypes.RC4Full) || Toolbox.HasEncryption(t, EncryptionTypes.RC4Header));
        }

        internal static IAsyncResult BeginCheckEncryption(PeerId id, AsyncCallback callback, object state)
        {
            return BeginCheckEncryption(id, callback, state, null);
        }

        internal static IAsyncResult BeginCheckEncryption(PeerId id, AsyncCallback callback, object state, byte[][] sKeys)
        {
            EncryptorAsyncResult result = new EncryptorAsyncResult(id, callback, state);
            result.SKeys = sKeys;

            IConnection c = id.Connection;
            try
            {
                // If the connection is incoming, receive the handshake before
                // trying to decide what encryption to use
                if (id.Connection.IsIncoming)
                {
                    result.Buffer = new byte[id.BytesToRecieve];
                    NetworkIO.EnqueueReceive(c, result.Buffer, 0, result.Buffer.Length, HandshakeReceivedCallback, result);
                }
                else
                {
                    bool hasRC4 = CheckRC4(id);
                    bool hasPlainText = Toolbox.HasEncryption(id.Engine.Settings.AllowedEncryption, EncryptionTypes.PlainText);

                    if (id.Engine.Settings.PreferEncryption)
                    {
                        if (hasRC4)
                        {
                            result.EncSocket = new PeerAEncryption(id.TorrentManager.Torrent.infoHash, EncryptionTypes.All);
                            result.EncSocket.BeginHandshake(id.Connection, CompletedEncryptedHandshakeCallback, result);
                        }
                        else
                        {
                            result.Complete();
                        }
                    }
                    else
                    {
                        if (hasPlainText)
                        {
                            result.Complete();
                        }
                        else
                        {
                            result.EncSocket = new PeerAEncryption(id.TorrentManager.Torrent.infoHash, EncryptionTypes.All);
                            result.EncSocket.BeginHandshake(id.Connection, CompletedEncryptedHandshakeCallback, result);
                        }
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
            EncryptorAsyncResult r = (EncryptorAsyncResult)result;

            if (!r.IsCompleted)
                r.AsyncWaitHandle.WaitOne();

            if (r == null)
                throw new ArgumentException("Invalid async result");

            if (r.SavedException != null)
                throw r.SavedException;

            r.Id.Encryptor = r.Encryptor;
            r.Id.Decryptor = r.Decryptor;
            initialData = r.InitialData;

            r.AsyncWaitHandle.Close();
        }

        private static void HandshakeReceived(bool succeeded, int count, object state)
        {
            EncryptorAsyncResult result = (EncryptorAsyncResult)state;
            IConnection connection = result.Id.Connection;

            try
            {
                if (!succeeded)
                    throw new EncryptionException("Couldn't receive the handshake");
                
                result.Available += count;
                if (count < result.Buffer.Length)
                {
                    NetworkIO.EnqueueReceive(connection, result.Buffer, result.Available, result.Buffer.Length - result.Available,
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
                    result.EncSocket = new PeerBEncryption(result.SKeys, EncryptionTypes.All);
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

            result.AsyncWaitHandle.Close();
            //r.AsyncWaitHandle.Close();
        }
    }
}
