//
// PeerBEncryption.cs
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
    /// Class to handle message stream encryption for receiving connections
    /// </summary>
    public class PeerBEncryption : EncryptedSocket
    {
        private byte[][] possibleSKEYs = null;
        private byte[] VerifyBytes;

        private AsyncCallback gotVerificationCallback;
        private AsyncCallback gotPadCCallback;
        private AsyncCallback gotInitialPayloadCallback;

        public PeerBEncryption(List<TorrentManager> torrents, EncryptionType minCryptoAllowed)
            : base(minCryptoAllowed)
        {
            possibleSKEYs = new byte[torrents.Count][];

            for (int i = 0; i < torrents.Count; i++)
            {
                possibleSKEYs[i] = torrents[i].Torrent.InfoHash;
            }

            gotVerificationCallback = new AsyncCallback(gotVerification);
            gotPadCCallback = new AsyncCallback(gotPadC);
            gotInitialPayloadCallback = new AsyncCallback(gotInitialPayload);
        }

        public PeerBEncryption(byte[][] possibleSKEYs, EncryptionType minCryptoAllowed)
            : base(minCryptoAllowed)
        {
            this.possibleSKEYs = possibleSKEYs;

            gotVerificationCallback = new AsyncCallback(gotVerification);
            gotPadCCallback = new AsyncCallback(gotPadC);
            gotInitialPayloadCallback = new AsyncCallback(gotInitialPayload);
        }

        public override void Start(Socket socket)
        {
            Logger.Log(id, "B: Start " + socket.Available);
            base.Start(socket);
        }

        protected override void doneReceiveY(IAsyncResult result)
        {
            base.doneReceiveY(result); // 1 A->B: Diffie Hellman Ya, PadA

            Logger.Log(id, "B: Step two");

            byte[] req1 = Hash(Encoding.ASCII.GetBytes("req1"), S);
            Synchronize(req1, 628); // 3 A->B: HASH('req1', S)
        }


        protected override void doneSynchronize(IAsyncResult result)
        {
            base.doneSynchronize(result);

            Logger.Log(id, " B: Synchronize");
            VerifyBytes = new byte[20 + VerificationConstant.Length + 4 + 2]; // ... HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), PadC, len(IA))

            ReceiveMessage(VerifyBytes, VerifyBytes.Length, gotVerificationCallback);
        }


        private void gotVerification(IAsyncResult result)
        {
            Logger.Log(id, " B: Verification");

            byte[] torrentHash = new byte[20];

            byte[] myVC = new byte[8];
            byte[] myCP = new byte[4];
            byte[] lenPadC = new byte[2];

            Array.Copy(VerifyBytes, 0, torrentHash, 0, torrentHash.Length); // HASH('req2', SKEY) xor HASH('req3', S)

            if (!MatchSKEY(torrentHash))
            {
                EncryptionError();
                return;
            }

            CreateCryptors("keyB", "keyA");

            DoDecrypt(VerifyBytes, 20, 14); // ENCRYPT(VC, ...

            Array.Copy(VerifyBytes, 20, myVC, 0, myVC.Length);
            if (!ToolBox.ByteMatch(myVC, VerificationConstant))
            {
                EncryptionError();
                return;
            }

            Array.Copy(VerifyBytes, 28, myCP, 0, myCP.Length); // ...crypto_provide ...
            if (SelectCrypto(myCP) == 0)
            {
                EncryptionError();
                return;
            }

            Array.Copy(VerifyBytes, 32, lenPadC, 0, lenPadC.Length); // ... len(padC) ...
            PadC = new byte[DeLen(lenPadC) + 2];
            ReceiveMessage(PadC, PadC.Length, gotPadCCallback); // padC
        }

        private void gotPadC(IAsyncResult result)
        {
            Logger.Log(id, "B: Pad"); // padC
            DoDecrypt(PadC, 0, PadC.Length);

            byte[] lenInitialPayload = new byte[2]; // ... len(IA))
            Array.Copy(PadC, PadC.Length - 2, lenInitialPayload, 0, 2);

            RemoteInitialPayload = new byte[DeLen(lenInitialPayload)]; // ... ENCRYPT(IA)
            ReceiveMessage(RemoteInitialPayload, RemoteInitialPayload.Length, gotInitialPayload);
        }

        private void gotInitialPayload(IAsyncResult result)
        {
            Logger.Log(id, "B: Payload");
            DoDecrypt(RemoteInitialPayload, 0, RemoteInitialPayload.Length); // ... ENCRYPT(IA)
            StepFour();
        }

        private void StepFour()
        {
            byte[] padD = GeneratePad();

            // 4 B->A: ENCRYPT(VC, crypto_select, len(padD), padD)
            SendMessage(DoEncrypt(VerificationConstant));
            SendMessage(DoEncrypt(CryptoSelect));
            SendMessage(DoEncrypt(Len(padD)));
            SendMessage(DoEncrypt(padD));

            Ready();
            Logger.Log(id, "B: Ready");
        }


        /// <summary>
        /// Matches a torrent based on whether the HASH('req2', SKEY) xor HASH('req3', S) matches, where SKEY is the InfoHash of the torrent
        /// and sets the SKEY to the InfoHash of the matched torrent.
        /// </summary>
        /// <returns>true if a match has been found</returns>
        private bool MatchSKEY(byte[] torrentHash)
        {
            bool match = false;

            for(int i = 0; i < possibleSKEYs.Length; i++)
            {
                byte[] req2 = Hash(Encoding.ASCII.GetBytes("req2"), possibleSKEYs[i]);
                byte[] req3 = Hash(Encoding.ASCII.GetBytes("req3"), S);

                for (int j = 0; j < req2.Length; j++)
                {
                    if (torrentHash[j] != (req2[j] ^ req3[j]))
                    {
                        match = false;
                        break;
                    }
                    else
                    {
                        match = true;
                    }
                }

                if (match)
                {
                    SKEY = possibleSKEYs[i];
                    return true;
                }
            }
            return false;
        }
    }
}
