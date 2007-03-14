//
// PeerAEncryption.cs
//
// Authors:
//   Yiduo Wang planetbeing@gmail.com
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
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    /// Class to handle message stream encryption for initiating connections
    /// </summary>
    public class PeerAEncryption : EncryptedSocket
    {
        private byte[] VerifyBytes;

        private AsyncCallback gotVerificationCallback;
        private AsyncCallback gotPadDCallback;

        public PeerAEncryption(byte[] InfoHash, EncryptionType minCryptoAllowed)
            : base(minCryptoAllowed)
        {
            this.id = id;
            gotVerificationCallback = new AsyncCallback(gotVerification);
            gotPadDCallback = new AsyncCallback(gotPadD);

            SKEY = InfoHash;
        }

        public override void Start(Socket socket)
        {
            Logger.Log(id, " A: Encryption started");
            base.Start(socket);
        }

        protected override void doneReceiveY(IAsyncResult result)
        {
            base.doneReceiveY(result); // 2 B->A: Diffie Hellman Yb, PadB

            StepThree();
        }

        private void StepThree()
        {
            Logger.Log(id, "A: Step three");

            CreateCryptors("keyA", "keyB");

            // 3 A->B: HASH('req1', S)
            byte[] req1 = Hash(Encoding.ASCII.GetBytes("req1"), S);

            // ... HASH('req2', SKEY)
            byte[] req2 = Hash(Encoding.ASCII.GetBytes("req2"), SKEY);

            // ... HASH('req3', S)
            byte[] req3 = Hash(Encoding.ASCII.GetBytes("req3"), S);

            // HASH('req2', SKEY) xor HASH('req3', S)
            for (int i = 0; i < req2.Length; i++)
                req2[i] ^= req3[i];

            byte[] padC = GeneratePad();

            // 3 A->B: HASH('req1', S), HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), ...
            SendMessage(req1);
            SendMessage(req2);
            SendMessage(DoEncrypt(VerificationConstant));
            SendMessage(DoEncrypt(CryptoProvide));
            SendMessage(DoEncrypt(Len(padC)));
            SendMessage(DoEncrypt(padC));

            // ... PadC, len(IA)), ENCRYPT(IA)
            Logger.Log(id, " A: Initial Payload - " + InitialPayload.Length);
            SendMessage(DoEncrypt(Len(InitialPayload)));
            SendMessage(DoEncrypt(InitialPayload));

            InitialPayload = new byte[0];

            Synchronize(DoDecrypt(VerificationConstant), 616); // 4 B->A: ENCRYPT(VC)
        }

        protected override void doneSynchronize(IAsyncResult result)
        {
            base.doneSynchronize(result); // 4 B->A: ENCRYPT(VC, ...

            Logger.Log(id, " A: Step five");

            VerifyBytes = new byte[4 + 2];
            ReceiveMessage(VerifyBytes, VerifyBytes.Length, gotVerificationCallback); // crypto_select, len(padD) ...
        }

        private void gotVerification(IAsyncResult result)
        {
            byte[] myCS = new byte[4];
            byte[] lenPadD = new byte[2];

            DoDecrypt(VerifyBytes, 0, VerifyBytes.Length);

            Array.Copy(VerifyBytes, 0, myCS, 0, myCS.Length); // crypto_select

            if (SelectCrypto(myCS) == 0)
            {
                EncryptionError();
                return;
            }

            Array.Copy(VerifyBytes, myCS.Length, lenPadD, 0, lenPadD.Length); // len(padD)

            PadD = new byte[DeLen(lenPadD)];

            ReceiveMessage(PadD, PadD.Length, gotPadDCallback);
        }

        private void gotPadD(IAsyncResult result)
        {
            DoDecrypt(PadD, 0, PadD.Length); // padD

            ready();
        }
    }
}
