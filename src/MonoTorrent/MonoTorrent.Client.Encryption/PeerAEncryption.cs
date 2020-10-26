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

using MonoTorrent.Client.Messages;

using ReusableTasks;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    /// Class to handle message stream encryption for initiating connections
    /// </summary>
    sealed class PeerAEncryption : EncryptedSocket
    {
        public byte[] InitialPayload { get; }

        public PeerAEncryption (InfoHash InfoHash, EncryptionTypes allowedEncryption)
            : this (InfoHash, allowedEncryption, null)
        {

        }

        public PeerAEncryption (InfoHash InfoHash, EncryptionTypes allowedEncryption, byte[] initialPayload)
            : base (allowedEncryption)
        {
            InitialPayload = initialPayload ?? Array.Empty<byte> ();
            SKEY = InfoHash;
        }

        protected override async ReusableTask DoneReceiveY ()
        {
            CreateCryptors (KeyABytes, KeyBBytes);

            // 3 A->B: HASH('req1', S)
            byte[] req1 = Hash (Req1Bytes, S);

            // ... HASH('req2', SKEY)
            byte[] req2 = Hash (Req2Bytes, SKEY.Hash);

            // ... HASH('req3', S)
            byte[] req3 = Hash (Req3Bytes, S);

            // HASH('req2', SKEY) xor HASH('req3', S)
            for (int i = 0; i < req2.Length; i++)
                req2[i] ^= req3[i];

            byte[] padC = GeneratePad ();

            // 3 A->B: HASH('req1', S), HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), ...
            int bufferLength = req1.Length + req2.Length + VerificationConstant.Length + CryptoProvide.Length
                             + 2 + padC.Length + 2 + InitialPayload.Length;

            using (NetworkIO.BufferPool.Rent (bufferLength, out ByteBuffer dataBuffer)) {
                var buffer = dataBuffer.Data;
                int offset = 0;
                offset += Message.Write (buffer, offset, req1);
                offset += Message.Write (buffer, offset, req2);
                offset += Message.Write (buffer, offset, VerificationConstant);
                DoEncrypt (buffer, offset - VerificationConstant.Length, VerificationConstant.Length);

                offset += Message.Write (buffer, offset, DoEncrypt (CryptoProvide));
                offset += Message.Write (buffer, offset, (short) padC.Length);
                DoEncrypt (buffer, offset - 2, 2);

                offset += Message.Write (buffer, offset, DoEncrypt (padC));

                // ... PadC, len(IA)), ENCRYPT(IA)
                offset += Message.Write (buffer, offset, (short) InitialPayload.Length);
                DoEncrypt (buffer, offset - 2, 2);

                offset += Message.Write (buffer, offset, DoEncrypt (InitialPayload));

                // Send the entire message in one go
                await NetworkIO.SendAsync (socket, dataBuffer, 0, bufferLength).ConfigureAwait (false);
            }
            DoDecrypt (VerificationConstant, 0, VerificationConstant.Length);
            await SynchronizeAsync (VerificationConstant, 616).ConfigureAwait (false); // 4 B->A: ENCRYPT(VC)
        }

        protected override async ReusableTask DoneSynchronizeAsync ()
        {
            // The first 4 bytes are the crypto selector. The last 2 bytes are the length of padD.
            int verifyBytesLength = 4 + 2;
            using (NetworkIO.BufferPool.Rent (verifyBytesLength, out ByteBuffer verifyBytes)) {
                await ReceiveMessageAsync (verifyBytes, verifyBytesLength).ConfigureAwait (false); // crypto_select, len(padD) ...
                DoDecrypt (verifyBytes.Data, 0, verifyBytesLength);

                short padDLength = Message.ReadShort (verifyBytes.Data, 4);
                using (NetworkIO.BufferPool.Rent (padDLength, out ByteBuffer padD)) {
                    await ReceiveMessageAsync (padD, padDLength).ConfigureAwait (false);
                    DoDecrypt (padD.Data, 0, padDLength);
                }
                SelectCrypto (verifyBytes.Data, true);
            }
        }
    }
}