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
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Client.Messages;
using ReusableTasks;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    /// Class to handle message stream encryption for receiving connections
    /// </summary>
    sealed class PeerBEncryption : EncryptedSocket
    {
        public byte[] InitialData { get; private set; }

        InfoHash[] PossibleSKEYs { get; }

        public PeerBEncryption(InfoHash[] possibleSKEYs, EncryptionTypes allowedEncryption)
            : base(allowedEncryption)
        {
            InitialData = Array.Empty<byte> ();
            PossibleSKEYs = possibleSKEYs;
        }

        protected override async ReusableTask doneReceiveY()
        {
            byte[] req1 = Hash(Req1Bytes, S);
            await Synchronize(req1, 628); // 3 A->B: HASH('req1', S)
        }

        protected override async ReusableTask doneSynchronize()
        {
            await base.doneSynchronize();

            var verifyBytes = new byte[20 + VerificationConstant.Length + 4 + 2]; // ... HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), PadC, len(IA))

            await ReceiveMessage(verifyBytes, verifyBytes.Length);
            await gotVerification(verifyBytes);
        }

        private async ReusableTask gotVerification(byte[] verifyBytes)
        {
            byte[] torrentHash = new byte[20];

            byte[] myCP = new byte[4];

            Array.Copy(verifyBytes, 0, torrentHash, 0, torrentHash.Length); // HASH('req2', SKEY) xor HASH('req3', S)

            if (!MatchSKEY(torrentHash))
                throw new EncryptionException("No valid SKey found");

            CreateCryptors(KeyBBytes, KeyABytes);

            DoDecrypt(verifyBytes, 20, 14); // ENCRYPT(VC, ...

            if (!Toolbox.ByteMatch(verifyBytes, 20, VerificationConstant, 0, VerificationConstant.Length))
                throw new EncryptionException("Verification constant was invalid");

            Array.Copy(verifyBytes, 28, myCP, 0, myCP.Length); // ...crypto_provide ...
                
            // We need to select the crypto *after* we send our response, otherwise the wrong
            // encryption will be used on the response
            int lenInitialPayload;
            var lenPadC = Message.ReadShort (verifyBytes, 32) + 2;
            var padC = ClientEngine.BufferPool.Rent (lenPadC);
            try {
                await ReceiveMessage(padC, lenPadC); // padC

                DoDecrypt(padC, 0, lenPadC);
                lenInitialPayload = Message.ReadShort (padC, lenPadC - 2);
            } finally {
                ClientEngine.BufferPool.Return (padC);
            }

            InitialData = new byte[lenInitialPayload]; // ... ENCRYPT(IA)
            await ReceiveMessage(InitialData, InitialData.Length);
            DoDecrypt(InitialData, 0, InitialData.Length); // ... ENCRYPT(IA)

            // Step Four
            byte[] padD = GeneratePad();
            SelectCrypto(myCP, false);

            // 4 B->A: ENCRYPT(VC, crypto_select, len(padD), padD)
            var finalBufferLength = VerificationConstant.Length + CryptoSelect.Length + 2 + padD.Length;
            byte[] buffer = ClientEngine.BufferPool.Rent (finalBufferLength);
                
            int offset = 0;
            offset += Message.Write(buffer, offset, VerificationConstant);
            offset += Message.Write(buffer, offset, CryptoSelect);
            offset += Message.Write(buffer, offset, Len(padD));
            offset += Message.Write(buffer, offset, padD);

            DoEncrypt(buffer, 0, finalBufferLength);
            try {
                await NetworkIO.SendAsync(socket, buffer, 0, finalBufferLength).ConfigureAwait (false);
            } finally {
                ClientEngine.BufferPool.Return (buffer);
            }

            SelectCrypto(myCP, true);
        }

        /// <summary>
        /// Matches a torrent based on whether the HASH('req2', SKEY) xor HASH('req3', S) matches, where SKEY is the InfoHash of the torrent
        /// and sets the SKEY to the InfoHash of the matched torrent.
        /// </summary>
        /// <returns>true if a match has been found</returns>
        private bool MatchSKEY(byte[] torrentHash)
        {
            for (int i = 0; i < PossibleSKEYs.Length; i++)
            {
                byte[] req2 = Hash(Req2Bytes, PossibleSKEYs[i].Hash);
                byte[] req3 = Hash(Req3Bytes, S);
                    
                bool match = true;
                for (int j = 0; j < req2.Length && match; j++)
                    match = torrentHash[j] == (req2[j] ^ req3[j]);

                if (match)
                {
                    SKEY = PossibleSKEYs[i];
                    return true;
                }
            }

            return false;
        }
    }
}