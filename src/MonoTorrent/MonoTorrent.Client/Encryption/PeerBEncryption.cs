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
using System.Net.Sockets;
using MonoTorrent.Common;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;

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

        public PeerBEncryption(byte[][] possibleSKEYs, EncryptionTypes allowedEncryption)
            : base(allowedEncryption)
        {
            this.possibleSKEYs = possibleSKEYs;

            gotVerificationCallback = new AsyncCallback(gotVerification);
            gotPadCCallback = new AsyncCallback(gotPadC);
            gotInitialPayloadCallback = new AsyncCallback(gotInitialPayload);
        }

        protected override void doneReceiveY(IAsyncResult result)
        {
            try
            {
                base.doneReceiveY(result); // 1 A->B: Diffie Hellman Ya, PadA

                byte[] req1 = Hash(Encoding.ASCII.GetBytes("req1"), S);
                Synchronize(req1, 628); // 3 A->B: HASH('req1', S)
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }


        protected override void doneSynchronize(IAsyncResult result)
        {
            try
            {
                base.doneSynchronize(result);

                VerifyBytes = new byte[20 + VerificationConstant.Length + 4 + 2]; // ... HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), PadC, len(IA))

                ReceiveMessage(VerifyBytes, VerifyBytes.Length, gotVerificationCallback);
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }

        byte[] b;
        private void gotVerification(IAsyncResult result)
        {
            try
            {
                byte[] torrentHash = new byte[20];

                byte[] myVC = new byte[8];
                byte[] myCP = new byte[4];
                byte[] lenPadC = new byte[2];

                Array.Copy(VerifyBytes, 0, torrentHash, 0, torrentHash.Length); // HASH('req2', SKEY) xor HASH('req3', S)

                if (!MatchSKEY(torrentHash))
                {
                    asyncResult.Complete(new EncryptionException("No valid SKey found"));
                    return;
                }

                CreateCryptors("keyB", "keyA");

                DoDecrypt(VerifyBytes, 20, 14); // ENCRYPT(VC, ...

                Array.Copy(VerifyBytes, 20, myVC, 0, myVC.Length);
                if (!Toolbox.ByteMatch(myVC, VerificationConstant))
                {
                    asyncResult.Complete(new EncryptionException("Verification constant was invalid"));
                    return;
                }

                Array.Copy(VerifyBytes, 28, myCP, 0, myCP.Length); // ...crypto_provide ...
                
                // We need to select the crypto *after* we send our response, otherwise the wrong
                // encryption will be used on the response
                b = myCP;
                Array.Copy(VerifyBytes, 32, lenPadC, 0, lenPadC.Length); // ... len(padC) ...
                PadC = new byte[DeLen(lenPadC) + 2];
                ReceiveMessage(PadC, PadC.Length, gotPadCCallback); // padC            
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }

        private void gotPadC(IAsyncResult result)
        {
            try
            {
                DoDecrypt(PadC, 0, PadC.Length);

                byte[] lenInitialPayload = new byte[2]; // ... len(IA))
                Array.Copy(PadC, PadC.Length - 2, lenInitialPayload, 0, 2);

                RemoteInitialPayload = new byte[DeLen(lenInitialPayload)]; // ... ENCRYPT(IA)
                ReceiveMessage(RemoteInitialPayload, RemoteInitialPayload.Length, gotInitialPayload);
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }

        private void gotInitialPayload(IAsyncResult result)
        {
            try
            {
                DoDecrypt(RemoteInitialPayload, 0, RemoteInitialPayload.Length); // ... ENCRYPT(IA)
                StepFour();
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }

        private void StepFour()
        {
            try
            {
                byte[] padD = GeneratePad();
                SelectCrypto(b, false);
                // 4 B->A: ENCRYPT(VC, crypto_select, len(padD), padD)
                byte[] buffer = new byte[VerificationConstant.Length + CryptoSelect.Length + 2 + padD.Length];
                
                int offset = 0;
                offset += Message.Write(buffer, offset, VerificationConstant);
                offset += Message.Write(buffer, offset, CryptoSelect);
                offset += Message.Write(buffer, offset, Len(padD));
                offset += Message.Write(buffer, offset, padD);

                DoEncrypt(buffer, 0, buffer.Length);
                SendMessage(buffer);

                SelectCrypto(b, true);

                Ready();
            }

            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }


        /// <summary>
        /// Matches a torrent based on whether the HASH('req2', SKEY) xor HASH('req3', S) matches, where SKEY is the InfoHash of the torrent
        /// and sets the SKEY to the InfoHash of the matched torrent.
        /// </summary>
        /// <returns>true if a match has been found</returns>
        private bool MatchSKEY(byte[] torrentHash)
        {
            try
            {
                for (int i = 0; i < possibleSKEYs.Length; i++)
                {
                    byte[] req2 = Hash(Encoding.ASCII.GetBytes("req2"), possibleSKEYs[i]);
                    byte[] req3 = Hash(Encoding.ASCII.GetBytes("req3"), S);
                    
                    bool match = true;
                    for (int j = 0; j < req2.Length && match; j++)
                        match = torrentHash[j] == (req2[j] ^ req3[j]);

                    if (match)
                    {
                        SKEY = possibleSKEYs[i];
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
            return false;
        }
    }
}