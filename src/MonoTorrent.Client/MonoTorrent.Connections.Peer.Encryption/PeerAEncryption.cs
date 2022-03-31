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
using System.Collections.Generic;

using MonoTorrent.Client;
using MonoTorrent.Messages;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer.Encryption
{
    /// <summary>
    /// Class to handle message stream encryption for initiating connections
    /// </summary>
    sealed class PeerAEncryption : EncryptedSocket
    {
        public ReadOnlyMemory<byte> InitialPayload { get; }

        public PeerAEncryption (Factories factories, InfoHash infoHash, IList<EncryptionType> allowedEncryption)
            : this (factories, infoHash, allowedEncryption, null)
        {

        }

        public PeerAEncryption (Factories factories, InfoHash infoHash, IList<EncryptionType> allowedEncryption, ReadOnlyMemory<byte> initialPayload)
            : base (factories, allowedEncryption)
        {
            if (allowedEncryption.Contains (EncryptionType.PlainText))
                throw new NotSupportedException ("'PlainText' is an unsupported RC4 encryption type.");

            InitialPayload = initialPayload;
            SKEY = infoHash;
        }

        protected override async ReusableTask DoneReceiveY ()
        {
            CreateCryptors (KeyABytes, KeyBBytes);

            // 3 A->B: HASH('req1', S)
            byte[] req1 = Hash (Req1Bytes, S!);

            // ... HASH('req2', SKEY)
            byte[] req2 = Hash (Req2Bytes, SKEY!.Span.ToArray ());

            // ... HASH('req3', S)
            byte[] req3 = Hash (Req3Bytes, S!);

            // HASH('req2', SKEY) xor HASH('req3', S)
            for (int i = 0; i < req2.Length; i++)
                req2[i] ^= req3[i];

            using var releaser = MemoryPool.Default.Rent (RandomNumber (512), out Memory<byte> padC);

            // 3 A->B: HASH('req1', S), HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), ...
            int bufferLength = req1.Length + req2.Length + VerificationConstant.Length + CryptoProvide.Length
                             + 2 + padC.Length + 2 + InitialPayload.Length;

            using (NetworkIO.BufferPool.Rent (bufferLength, out Memory<byte> buffer)) {
                var position = buffer;
                Message.Write (ref position, req1);
                Message.Write (ref position, req2);

                var before = position;
                Message.Write (ref position, VerificationConstant);
                Message.Write (ref position, CryptoProvide);
                Message.Write (ref position, (short) padC.Length);
                Message.Write (ref position, padC.Span);
                Message.Write (ref position, (short) InitialPayload.Length);
                Message.Write (ref position, InitialPayload.Span);
                DoEncrypt (before.Span);

                await NetworkIO.SendAsync (socket!, buffer).ConfigureAwait (false);
            }
            DoDecrypt (VerificationConstant);
            await SynchronizeAsync (VerificationConstant, 616).ConfigureAwait (false); // 4 B->A: ENCRYPT(VC)
        }

        protected override async ReusableTask DoneSynchronizeAsync ()
        {
            // The first 4 bytes are the crypto selector. The last 2 bytes are the length of padD.
            int verifyBytesLength = 4 + 2;
            using (NetworkIO.BufferPool.Rent (verifyBytesLength, out Memory<byte> verifyBytes)) {
                await ReceiveMessageAsync (verifyBytes).ConfigureAwait (false); // crypto_select, len(padD) ...
                DoDecrypt (verifyBytes.Span);

                short padDLength = Message.ReadShort (verifyBytes.Span.Slice (4));
                using (NetworkIO.BufferPool.Rent (padDLength, out Memory<byte> padD)) {

                    await ReceiveMessageAsync (padD).ConfigureAwait (false);
                    DoDecrypt (padD.Span);
                }
                SelectCrypto (verifyBytes.Span.Slice (0, 4), true);
            }
        }
    }
}
