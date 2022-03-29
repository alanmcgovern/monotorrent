//
// PeerBEncryption.cs
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
    /// Class to handle message stream encryption for receiving connections
    /// </summary>
    sealed class PeerBEncryption : EncryptedSocket
    {
        public byte[] InitialData { get; private set; }

        InfoHash[] PossibleSKEYs { get; }

        public PeerBEncryption (Factories factories, InfoHash[] possibleSKEYs, IList<EncryptionType> allowedEncryption)
            : base (factories, allowedEncryption)
        {
            InitialData = Array.Empty<byte> ();
            PossibleSKEYs = possibleSKEYs;
        }

        protected override async ReusableTask DoneReceiveY ()
        {
            byte[] req1 = Hash (Req1Bytes, S!);
            await SynchronizeAsync (req1, 628).ConfigureAwait (false); // 3 A->B: HASH('req1', S)
        }

        protected override async ReusableTask DoneSynchronizeAsync ()
        {
            // ... HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), PadC, len(IA))
            var length = 20 + VerificationConstant.Length + 4 + 2;
            using (NetworkIO.BufferPool.Rent (length, out Memory<byte> verifyBytes)) {
                await ReceiveMessageAsync (verifyBytes).ConfigureAwait (false);
                await GotVerification (verifyBytes).ConfigureAwait (false);
            }
        }

        async ReusableTask GotVerification (Memory<byte> verifyBytes)
        {
            var infoHash = verifyBytes.Slice (0, 20);
            var verificationConstant = verifyBytes.Slice (20, 8);
            var myCP = verifyBytes.Slice (28, 4);
            var padCSpan = verifyBytes.Slice (32, 2);

            if (!MatchSKEY (infoHash.Span))
                throw new EncryptionException ("No valid SKey found");

            // Create the encryptor/decryptors
            CreateCryptors (KeyBBytes, KeyABytes);

            // Decrypt everything after the infohash.
            DoDecrypt (verifyBytes.Slice (infoHash.Length).Span);

            if (!verificationConstant.Span.SequenceEqual (VerificationConstant))
                throw new EncryptionException ("Verification constant was invalid");

            // We need to select the crypto *after* we send our response, otherwise the wrong
            // encryption will be used on the response
            int lenInitialPayload;
            int lenPadC = Message.ReadShort (padCSpan.Span) + 2;
            using (NetworkIO.BufferPool.Rent (lenPadC, out Memory<byte> padC)) {
                await ReceiveMessageAsync (padC).ConfigureAwait (false); // padC
                DoDecrypt (padC.Span);
                lenInitialPayload = Message.ReadShort (padC.Span.Slice (lenPadC - 2, 2));
            }

            InitialData = new byte[lenInitialPayload]; // ... ENCRYPT(IA)
            using (NetworkIO.BufferPool.Rent (InitialData.Length, out Memory<byte> receiveBuffer)) {
                await ReceiveMessageAsync (receiveBuffer).ConfigureAwait (false);
                receiveBuffer.CopyTo (InitialData);
                DoDecrypt (InitialData); // ... ENCRYPT(IA)
            }

            // Step Four
            using var releaser = MemoryPool.Default.Rent (RandomNumber (512), out Memory<byte> padD);
            SelectCrypto (myCP.Span, false);

            // 4 B->A: ENCRYPT(VC, crypto_select, len(padD), padD)
            int finalBufferLength = VerificationConstant.Length + CryptoSelect!.Length + 2 + padD.Length;
            using (NetworkIO.BufferPool.Rent (finalBufferLength, out Memory<byte> buffer)) {
                var position = buffer;
                Message.Write (ref position, VerificationConstant);
                Message.Write (ref position, CryptoSelect);
                Message.Write (ref position, (short) padD.Length);
                Message.Write (ref position, padD.Span);
                DoEncrypt (buffer.Span);

                await NetworkIO.SendAsync (socket!, buffer).ConfigureAwait (false);
            }

            SelectCrypto (myCP.Span, true);
        }

        /// <summary>
        /// Matches a torrent based on whether the HASH('req2', SKEY) xor HASH('req3', S) matches, where SKEY is the InfoHash of the torrent
        /// and sets the SKEY to the InfoHash of the matched torrent.
        /// </summary>
        /// <returns>true if a match has been found</returns>
        bool MatchSKEY (ReadOnlySpan<byte> torrentHash)
        {
            for (int i = 0; i < PossibleSKEYs.Length; i++) {
                byte[] req2 = Hash (Req2Bytes, PossibleSKEYs[i].Span.ToArray ());
                byte[] req3 = Hash (Req3Bytes, S!);

                bool match = true;
                for (int j = 0; j < req2.Length && match; j++)
                    match = torrentHash[j] == (req2[j] ^ req3[j]);

                if (match) {
                    SKEY = PossibleSKEYs[i];
                    return true;
                }
            }

            return false;
        }
    }
}
