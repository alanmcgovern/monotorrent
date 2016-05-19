//
// PeerAEncryption.cs
//
// Authors:
//   Yiduo Wang planetbeing@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2007 Yiduo Wang
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
using MonoTorrent.Common;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    /// Class to handle message stream encryption for initiating connections
    /// </summary>
    class PeerAEncryption : EncryptedSocket
    {
        private byte[] VerifyBytes;

        private AsyncCallback gotVerificationCallback;
        private AsyncCallback gotPadDCallback;

        public PeerAEncryption(InfoHash InfoHash, EncryptionTypes allowedEncryption)
            : base(allowedEncryption)
        {
            gotVerificationCallback = new AsyncCallback(gotVerification);
            gotPadDCallback = new AsyncCallback(gotPadD);

            SKEY = InfoHash;
        }

        protected override void doneReceiveY()
        {
            try
            {
                base.doneReceiveY(); // 2 B->A: Diffie Hellman Yb, PadB

                StepThree();
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }

        private void StepThree()
        {
            try
            {
                CreateCryptors("keyA", "keyB");

                // 3 A->B: HASH('req1', S)
                byte[] req1 = Hash(Encoding.ASCII.GetBytes("req1"), S);

                // ... HASH('req2', SKEY)
                byte[] req2 = Hash(Encoding.ASCII.GetBytes("req2"), SKEY.Hash);

                // ... HASH('req3', S)
                byte[] req3 = Hash(Encoding.ASCII.GetBytes("req3"), S);

                // HASH('req2', SKEY) xor HASH('req3', S)
                for (int i = 0; i < req2.Length; i++)
                    req2[i] ^= req3[i];

                byte[] padC = GeneratePad();

                // 3 A->B: HASH('req1', S), HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), ...
                byte[] buffer = new byte[req1.Length + req2.Length + VerificationConstant.Length + CryptoProvide.Length
                                        + 2 + padC.Length + 2 + InitialPayload.Length];
                
                int offset = 0;
                offset += Message.Write(buffer, offset, req1);
                offset += Message.Write(buffer, offset, req2);
                offset += Message.Write(buffer, offset, DoEncrypt(VerificationConstant));
                offset += Message.Write(buffer, offset, DoEncrypt(CryptoProvide));
                offset += Message.Write(buffer, offset, DoEncrypt(Len(padC)));
                offset += Message.Write(buffer, offset, DoEncrypt(padC));

                // ... PadC, len(IA)), ENCRYPT(IA)
                offset += Message.Write(buffer, offset, DoEncrypt(Len(InitialPayload)));
                offset += Message.Write(buffer, offset, DoEncrypt(InitialPayload));
                
                // Send the entire message in one go
                SendMessage(buffer);
                InitialPayload = BufferManager.EmptyBuffer;

                Synchronize(DoDecrypt(VerificationConstant), 616); // 4 B->A: ENCRYPT(VC)
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }

        protected override void doneSynchronize()
        {
            try
            {
                base.doneSynchronize(); // 4 B->A: ENCRYPT(VC, ...

                VerifyBytes = new byte[4 + 2];
                ReceiveMessage(VerifyBytes, VerifyBytes.Length, gotVerificationCallback); // crypto_select, len(padD) ...
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }
        private byte[] b;
        private void gotVerification(IAsyncResult result)
        {
            try
            {
                byte[] myCS = new byte[4];
                byte[] lenPadD = new byte[2];

                DoDecrypt(VerifyBytes, 0, VerifyBytes.Length);

                Array.Copy(VerifyBytes, 0, myCS, 0, myCS.Length); // crypto_select

                //SelectCrypto(myCS);
                b = myCS;
                Array.Copy(VerifyBytes, myCS.Length, lenPadD, 0, lenPadD.Length); // len(padD)

                PadD = new byte[DeLen(lenPadD)];

                ReceiveMessage(PadD, PadD.Length, gotPadDCallback);
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }

        private void gotPadD(IAsyncResult result)
        {
            try
            {
                DoDecrypt(PadD, 0, PadD.Length); // padD
                SelectCrypto(b, true);
                Ready();
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }
    }
}