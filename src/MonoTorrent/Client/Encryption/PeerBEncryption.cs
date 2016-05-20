using System;
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    ///     Class to handle message stream encryption for receiving connections
    /// </summary>
    internal class PeerBEncryption : EncryptedSocket
    {
        private readonly AsyncCallback gotPadCCallback;

        private readonly AsyncCallback gotVerificationCallback;
        private readonly InfoHash[] possibleSKEYs;
        private byte[] b;
        private byte[] VerifyBytes;

        public PeerBEncryption(InfoHash[] possibleSKEYs, EncryptionTypes allowedEncryption)
            : base(allowedEncryption)
        {
            this.possibleSKEYs = possibleSKEYs;

            gotVerificationCallback = gotVerification;
            gotPadCCallback = gotPadC;
        }

        protected override void doneReceiveY()
        {
            try
            {
                base.doneReceiveY(); // 1 A->B: Diffie Hellman Ya, PadA

                var req1 = Hash(Encoding.ASCII.GetBytes("req1"), S);
                Synchronize(req1, 628); // 3 A->B: HASH('req1', S)
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
                base.doneSynchronize();

                VerifyBytes = new byte[20 + VerificationConstant.Length + 4 + 2];
                // ... HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), PadC, len(IA))

                ReceiveMessage(VerifyBytes, VerifyBytes.Length, gotVerificationCallback);
            }
            catch (Exception ex)
            {
                asyncResult.Complete(ex);
            }
        }

        private void gotVerification(IAsyncResult result)
        {
            try
            {
                var torrentHash = new byte[20];

                var myVC = new byte[8];
                var myCP = new byte[4];
                var lenPadC = new byte[2];

                Array.Copy(VerifyBytes, 0, torrentHash, 0, torrentHash.Length);
                // HASH('req2', SKEY) xor HASH('req3', S)

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

                var lenInitialPayload = new byte[2]; // ... len(IA))
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
                var padD = GeneratePad();
                SelectCrypto(b, false);
                // 4 B->A: ENCRYPT(VC, crypto_select, len(padD), padD)
                var buffer = new byte[VerificationConstant.Length + CryptoSelect.Length + 2 + padD.Length];

                var offset = 0;
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
        ///     Matches a torrent based on whether the HASH('req2', SKEY) xor HASH('req3', S) matches, where SKEY is the InfoHash
        ///     of the torrent
        ///     and sets the SKEY to the InfoHash of the matched torrent.
        /// </summary>
        /// <returns>true if a match has been found</returns>
        private bool MatchSKEY(byte[] torrentHash)
        {
            try
            {
                for (var i = 0; i < possibleSKEYs.Length; i++)
                {
                    var req2 = Hash(Encoding.ASCII.GetBytes("req2"), possibleSKEYs[i].Hash);
                    var req3 = Hash(Encoding.ASCII.GetBytes("req3"), S);

                    var match = true;
                    for (var j = 0; j < req2.Length && match; j++)
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