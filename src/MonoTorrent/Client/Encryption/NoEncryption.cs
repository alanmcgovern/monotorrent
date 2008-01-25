//
// NoEncryption.cs
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

namespace MonoTorrent.Client.Encryption
{
    public class NoEncryption : IEncryptorInternal
    {
        PeerIdInternal id;

        private EncryptorReadyHandler encryptorReady;
        private EncryptorIOErrorHandler encryptorIOError;
        private EncryptorEncryptionErrorHandler encryptorEncryptionError;


        event EncryptorReadyHandler IEncryptorInternal.EncryptorReady
        {
            add { encryptorReady += value; }
            remove { encryptorReady -= value; }
        }

        event EncryptorIOErrorHandler IEncryptorInternal.EncryptorIOError
        {
            add { encryptorIOError += value; }
            remove { encryptorIOError -= value; }
        }

        event EncryptorEncryptionErrorHandler IEncryptorInternal.EncryptorEncryptionError
        {
            add { encryptorEncryptionError += value; }
            remove { encryptorEncryptionError -= value; }
        }

        public NoEncryption()
        {

        }

        void IEncryptorInternal.Encrypt(byte[] buffer, int offset, int count)
        {

        }

        void IEncryptorInternal.Decrypt(byte[] buffer, int offset, int count)
        {

        }

        void IEncryptorInternal.AddInitialData(byte[] buffer, int offset, int count)
        {

        }

        void IEncryptorInternal.Start(IConnection socket)
        {
            Start(socket);
        }

        void IEncryptorInternal.Start(IConnection socket, byte[] initialBuffer, int offset, int count)
        {
            Start(socket);
        }

        private void Start(IConnection s)
        {
            encryptorReady(id);
        }

        bool IEncryptorInternal.IsReady()
        {
            return true;
        }

        bool IEncryptorInternal.IsInitialDataAvailable()
        {
            return false;
        }

        int IEncryptorInternal.GetInitialData(byte[] buffer, int offset, int count)
        {
            return 0;
        }

        void IEncryptorInternal.SetPeerConnectionID(PeerIdInternal id)
        {
            this.id = id;
        }
    }
}
