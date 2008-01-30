//
// IEncryptor.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Text;
using System.Net.Sockets;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client.Encryption
{
    internal delegate void EncryptorReadyHandler(PeerIdInternal id);
    internal delegate void EncryptorIOErrorHandler(PeerIdInternal id);
    internal delegate void EncryptorEncryptionErrorHandler(PeerIdInternal id);

    public interface IEncryptor
    {
    }

    internal interface IEncryptorInternal : IEncryptor
    {
        event EncryptorReadyHandler EncryptorReady;
        event EncryptorIOErrorHandler EncryptorIOError;
        event EncryptorEncryptionErrorHandler EncryptorEncryptionError;

        void Encrypt(byte[] buffer, int offset, int count);

        void Decrypt(byte[] buffer, int offset, int count);

        void AddInitialData(byte[] buffer, int offset, int count);

        void Start(IConnection socket);

        void Start(IConnection socket, byte[] initialBuffer, int offset, int count);

        bool IsReady();

        bool IsInitialDataAvailable();

        int GetInitialData(byte[] buffer, int offset, int count);

        void SetPeerConnectionID(PeerIdInternal id);
    }
}
