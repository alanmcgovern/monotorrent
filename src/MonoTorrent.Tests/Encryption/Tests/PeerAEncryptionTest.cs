using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Net.Sockets;
using MonoTorrent.Client.Encryption;
using System.Security.Cryptography;
using MonoTorrent.Common;

namespace MonoTorrent.Tests.Encryption.Tests
{
    [TestFixture]
    public class PeerAEncryptionTest
    {
        RandomNumberGenerator random = RandomNumberGenerator.Create();
        Random random2 = new Random();
        SHA1 sha1 = SHA1.Create();
        bool onReady;
        bool onEncryptionError;
        bool onIOError;

        [Test]
        public void WithPayLoad()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            byte[] initialPayload = new byte[random2.Next(1, 1000)];
            random.GetBytes(initialPayload);

            SocketPipe pipe = new SocketPipe();
            byte[] InfoHash = new byte[20];
            random.GetBytes(InfoHash);

            PeerAEncryption peerAEncryption = new PeerAEncryption(InfoHash, EncryptionType.RC4Full);
            peerAEncryption.SetPeerConnectionID(null);
            peerAEncryption.onEncryptorReady += new EncryptorReadyHandler(peerAEncryption_onEncryptorReady);
            peerAEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerAEncryption_onEncryptorIOError);
            peerAEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerAEncryption_onEncryptorEncryptionError);

            peerAEncryption.AddInitialData(initialPayload, 0, initialPayload.Length);
            peerAEncryption.Start(pipe.ClientSocket);

            pipe.ServerSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ServerSocket.Send(Y);
            pipe.ServerSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ServerSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);

            byte[] req1 = sha1.ComputeHash(req1S);

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 532; i++)
            {
                pipe.ServerSocket.Receive(byteBuffer);
                if (byteBuffer[0] == req1[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == req1.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 20);

            byte[] req2S = new byte[4 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("req2"), req2S, 4);
            Array.Copy(InfoHash, 0, req2S, 4, 20);

            byte[] req3S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req3"), req3S, 4);
            Array.Copy(S, 0, req3S, 4, 96);

            byte[] req2 = sha1.ComputeHash(req2S);
            byte[] req3 = sha1.ComputeHash(req3S);

            for (int i = 0; i < 20; i++)
            {
                req2[i] ^= req3[i];
            }

            byte[] req2r = new byte[20];
            pipe.ServerSocket.Receive(req2r);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(req2, req2r));

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = new byte[8];
            pipe.ServerSocket.Receive(vc);
            decryptor.InPlaceCrypt(vc, 0, 8);

            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(vc[i] == 0);
            }

            byte[] cp = new byte[4];
            pipe.ServerSocket.Receive(cp);
            decryptor.InPlaceCrypt(cp, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cp[i] == 0);
            }

            Assert.IsTrue(cp[3] == 2);

            byte[] lenPadC = new byte[2];
            pipe.ServerSocket.Receive(lenPadC);
            decryptor.InPlaceCrypt(lenPadC, 0, 2);

            byte[] padC = new byte[(lenPadC[0] << 8) + lenPadC[1]];
            pipe.ServerSocket.Receive(padC);
            decryptor.InPlaceCrypt(padC, 0, padC.Length);

            for (int i = 0; i < padC.Length; i++)
            {
                Assert.IsTrue(padC[i] == 0);
            }

            byte[] lenIA = new byte[2];
            pipe.ServerSocket.Receive(lenIA);
            decryptor.InPlaceCrypt(lenIA, 0, 2);

            byte[] initialPayloadr = new byte[(lenIA[0] << 8) + lenIA[1]];
            pipe.ServerSocket.Receive(initialPayloadr);
            decryptor.InPlaceCrypt(initialPayloadr, 0, initialPayloadr.Length);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(initialPayload, initialPayloadr));

            encryptor.InPlaceCrypt(vc, 0, 8);
            encryptor.InPlaceCrypt(cp, 0, 4);
            encryptor.InPlaceCrypt(lenPadC, 0, 2);
            encryptor.InPlaceCrypt(padC, 0, padC.Length);

            pipe.ServerSocket.Send(vc);
            pipe.ServerSocket.Send(cp);
            pipe.ServerSocket.Send(lenPadC);
            pipe.ServerSocket.Send(padC);

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsFalse(onEncryptionError);
            Assert.IsTrue(onReady);
            Assert.IsTrue(peerAEncryption.IsReady());

            byte[] payload = new byte[random2.Next(1, 1000)];
            random.GetBytes(payload);

            byte[] payloadr = new byte[payload.Length];
            payload.CopyTo(payloadr, 0);

            peerAEncryption.Encrypt(payloadr, 0, payloadr.Length);
            pipe.ClientSocket.Send(payloadr);
            pipe.ServerSocket.Receive(payloadr);
            decryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

            encryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            pipe.ServerSocket.Send(payloadr);
            pipe.ClientSocket.Receive(payloadr);
            peerAEncryption.Decrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

        }

        [Test]
        public void WithPlaintextAllowed()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            byte[] initialPayload = new byte[random2.Next(1, 1000)];
            random.GetBytes(initialPayload);

            SocketPipe pipe = new SocketPipe();
            byte[] InfoHash = new byte[20];
            random.GetBytes(InfoHash);

            PeerAEncryption peerAEncryption = new PeerAEncryption(InfoHash, EncryptionType.RC4Header);
            peerAEncryption.SetPeerConnectionID(null);
            peerAEncryption.onEncryptorReady += new EncryptorReadyHandler(peerAEncryption_onEncryptorReady);
            peerAEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerAEncryption_onEncryptorIOError);
            peerAEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerAEncryption_onEncryptorEncryptionError);

            peerAEncryption.AddInitialData(initialPayload, 0, initialPayload.Length);
            peerAEncryption.Start(pipe.ClientSocket);

            pipe.ServerSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ServerSocket.Send(Y);
            pipe.ServerSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ServerSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);

            byte[] req1 = sha1.ComputeHash(req1S);

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 532; i++)
            {
                pipe.ServerSocket.Receive(byteBuffer);
                if (byteBuffer[0] == req1[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == req1.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 20);

            byte[] req2S = new byte[4 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("req2"), req2S, 4);
            Array.Copy(InfoHash, 0, req2S, 4, 20);

            byte[] req3S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req3"), req3S, 4);
            Array.Copy(S, 0, req3S, 4, 96);

            byte[] req2 = sha1.ComputeHash(req2S);
            byte[] req3 = sha1.ComputeHash(req3S);

            for (int i = 0; i < 20; i++)
            {
                req2[i] ^= req3[i];
            }

            byte[] req2r = new byte[20];
            pipe.ServerSocket.Receive(req2r);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(req2, req2r));

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = new byte[8];
            pipe.ServerSocket.Receive(vc);
            decryptor.InPlaceCrypt(vc, 0, 8);

            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(vc[i] == 0);
            }

            byte[] cp = new byte[4];
            pipe.ServerSocket.Receive(cp);
            decryptor.InPlaceCrypt(cp, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cp[i] == 0);
            }

            Assert.IsTrue(cp[3] == 3);

            byte[] lenPadC = new byte[2];
            pipe.ServerSocket.Receive(lenPadC);
            decryptor.InPlaceCrypt(lenPadC, 0, 2);

            byte[] padC = new byte[(lenPadC[0] << 8) + lenPadC[1]];
            pipe.ServerSocket.Receive(padC);
            decryptor.InPlaceCrypt(padC, 0, padC.Length);

            for (int i = 0; i < padC.Length; i++)
            {
                Assert.IsTrue(padC[i] == 0);
            }

            byte[] lenIA = new byte[2];
            pipe.ServerSocket.Receive(lenIA);
            decryptor.InPlaceCrypt(lenIA, 0, 2);

            byte[] initialPayloadr = new byte[(lenIA[0] << 8) + lenIA[1]];
            pipe.ServerSocket.Receive(initialPayloadr);
            decryptor.InPlaceCrypt(initialPayloadr, 0, initialPayloadr.Length);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(initialPayload, initialPayloadr));

            cp[3] = 1;

            encryptor.InPlaceCrypt(vc, 0, 8);
            encryptor.InPlaceCrypt(cp, 0, 4);
            encryptor.InPlaceCrypt(lenPadC, 0, 2);
            encryptor.InPlaceCrypt(padC, 0, padC.Length);

            pipe.ServerSocket.Send(vc);
            pipe.ServerSocket.Send(cp);
            pipe.ServerSocket.Send(lenPadC);
            pipe.ServerSocket.Send(padC);

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsFalse(onEncryptionError);
            Assert.IsTrue(onReady);
            Assert.IsTrue(peerAEncryption.IsReady());

            byte[] payload = new byte[random2.Next(1, 1000)];
            random.GetBytes(payload);

            byte[] payloadr = new byte[payload.Length];
            payload.CopyTo(payloadr, 0);

            peerAEncryption.Encrypt(payloadr, 0, payloadr.Length);
            pipe.ClientSocket.Send(payloadr);
            pipe.ServerSocket.Receive(payloadr);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

            pipe.ServerSocket.Send(payloadr);
            pipe.ClientSocket.Receive(payloadr);
            peerAEncryption.Decrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

        }


        [Test]
        public void WithPlaintextNotAllowed()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            byte[] initialPayload = new byte[random2.Next(1, 1000)];
            random.GetBytes(initialPayload);

            SocketPipe pipe = new SocketPipe();
            byte[] InfoHash = new byte[20];
            random.GetBytes(InfoHash);

            PeerAEncryption peerAEncryption = new PeerAEncryption(InfoHash, EncryptionType.RC4Full);
            peerAEncryption.SetPeerConnectionID(null);
            peerAEncryption.onEncryptorReady += new EncryptorReadyHandler(peerAEncryption_onEncryptorReady);
            peerAEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerAEncryption_onEncryptorIOError);
            peerAEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerAEncryption_onEncryptorEncryptionError);

            peerAEncryption.AddInitialData(initialPayload, 0, initialPayload.Length);
            peerAEncryption.Start(pipe.ClientSocket);

            pipe.ServerSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ServerSocket.Send(Y);
            pipe.ServerSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ServerSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);

            byte[] req1 = sha1.ComputeHash(req1S);

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 532; i++)
            {
                pipe.ServerSocket.Receive(byteBuffer);
                if (byteBuffer[0] == req1[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == req1.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 20);

            byte[] req2S = new byte[4 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("req2"), req2S, 4);
            Array.Copy(InfoHash, 0, req2S, 4, 20);

            byte[] req3S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req3"), req3S, 4);
            Array.Copy(S, 0, req3S, 4, 96);

            byte[] req2 = sha1.ComputeHash(req2S);
            byte[] req3 = sha1.ComputeHash(req3S);

            for (int i = 0; i < 20; i++)
            {
                req2[i] ^= req3[i];
            }

            byte[] req2r = new byte[20];
            pipe.ServerSocket.Receive(req2r);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(req2, req2r));

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = new byte[8];
            pipe.ServerSocket.Receive(vc);
            decryptor.InPlaceCrypt(vc, 0, 8);

            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(vc[i] == 0);
            }

            byte[] cp = new byte[4];
            pipe.ServerSocket.Receive(cp);
            decryptor.InPlaceCrypt(cp, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cp[i] == 0);
            }

            Assert.IsTrue(cp[3] == 2);

            byte[] lenPadC = new byte[2];
            pipe.ServerSocket.Receive(lenPadC);
            decryptor.InPlaceCrypt(lenPadC, 0, 2);

            byte[] padC = new byte[(lenPadC[0] << 8) + lenPadC[1]];
            pipe.ServerSocket.Receive(padC);
            decryptor.InPlaceCrypt(padC, 0, padC.Length);

            for (int i = 0; i < padC.Length; i++)
            {
                Assert.IsTrue(padC[i] == 0);
            }

            byte[] lenIA = new byte[2];
            pipe.ServerSocket.Receive(lenIA);
            decryptor.InPlaceCrypt(lenIA, 0, 2);

            byte[] initialPayloadr = new byte[(lenIA[0] << 8) + lenIA[1]];
            pipe.ServerSocket.Receive(initialPayloadr);
            decryptor.InPlaceCrypt(initialPayloadr, 0, initialPayloadr.Length);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(initialPayload, initialPayloadr));

            cp[3] = 1;

            encryptor.InPlaceCrypt(vc, 0, 8);
            encryptor.InPlaceCrypt(cp, 0, 4);
            encryptor.InPlaceCrypt(lenPadC, 0, 2);
            encryptor.InPlaceCrypt(padC, 0, padC.Length);

            pipe.ServerSocket.Send(vc);
            pipe.ServerSocket.Send(cp);
            pipe.ServerSocket.Send(lenPadC);
            pipe.ServerSocket.Send(padC);

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsTrue(onEncryptionError);
            Assert.IsFalse(onReady);
            Assert.IsFalse(peerAEncryption.IsReady());
        }

        [Test]
        public void WithUnsupportedEncryption()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            byte[] initialPayload = new byte[random2.Next(1, 1000)];
            random.GetBytes(initialPayload);

            SocketPipe pipe = new SocketPipe();
            byte[] InfoHash = new byte[20];
            random.GetBytes(InfoHash);

            PeerAEncryption peerAEncryption = new PeerAEncryption(InfoHash, EncryptionType.RC4Full);
            peerAEncryption.SetPeerConnectionID(null);
            peerAEncryption.onEncryptorReady += new EncryptorReadyHandler(peerAEncryption_onEncryptorReady);
            peerAEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerAEncryption_onEncryptorIOError);
            peerAEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerAEncryption_onEncryptorEncryptionError);

            peerAEncryption.AddInitialData(initialPayload, 0, initialPayload.Length);
            peerAEncryption.Start(pipe.ClientSocket);

            pipe.ServerSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ServerSocket.Send(Y);
            pipe.ServerSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ServerSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);

            byte[] req1 = sha1.ComputeHash(req1S);

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 532; i++)
            {
                pipe.ServerSocket.Receive(byteBuffer);
                if (byteBuffer[0] == req1[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == req1.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 20);

            byte[] req2S = new byte[4 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("req2"), req2S, 4);
            Array.Copy(InfoHash, 0, req2S, 4, 20);

            byte[] req3S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req3"), req3S, 4);
            Array.Copy(S, 0, req3S, 4, 96);

            byte[] req2 = sha1.ComputeHash(req2S);
            byte[] req3 = sha1.ComputeHash(req3S);

            for (int i = 0; i < 20; i++)
            {
                req2[i] ^= req3[i];
            }

            byte[] req2r = new byte[20];
            pipe.ServerSocket.Receive(req2r);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(req2, req2r));

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = new byte[8];
            pipe.ServerSocket.Receive(vc);
            decryptor.InPlaceCrypt(vc, 0, 8);

            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(vc[i] == 0);
            }

            byte[] cp = new byte[4];
            pipe.ServerSocket.Receive(cp);
            decryptor.InPlaceCrypt(cp, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cp[i] == 0);
            }

            Assert.IsTrue(cp[3] == 2);

            byte[] lenPadC = new byte[2];
            pipe.ServerSocket.Receive(lenPadC);
            decryptor.InPlaceCrypt(lenPadC, 0, 2);

            byte[] padC = new byte[(lenPadC[0] << 8) + lenPadC[1]];
            pipe.ServerSocket.Receive(padC);
            decryptor.InPlaceCrypt(padC, 0, padC.Length);

            for (int i = 0; i < padC.Length; i++)
            {
                Assert.IsTrue(padC[i] == 0);
            }

            byte[] lenIA = new byte[2];
            pipe.ServerSocket.Receive(lenIA);
            decryptor.InPlaceCrypt(lenIA, 0, 2);

            byte[] initialPayloadr = new byte[(lenIA[0] << 8) + lenIA[1]];
            pipe.ServerSocket.Receive(initialPayloadr);
            decryptor.InPlaceCrypt(initialPayloadr, 0, initialPayloadr.Length);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(initialPayload, initialPayloadr));

            cp[1] = 1;
            cp[3] = 0;

            encryptor.InPlaceCrypt(vc, 0, 8);
            encryptor.InPlaceCrypt(cp, 0, 4);
            encryptor.InPlaceCrypt(lenPadC, 0, 2);
            encryptor.InPlaceCrypt(padC, 0, padC.Length);

            pipe.ServerSocket.Send(vc);
            pipe.ServerSocket.Send(cp);
            pipe.ServerSocket.Send(lenPadC);
            pipe.ServerSocket.Send(padC);

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsTrue(onEncryptionError);
            Assert.IsFalse(onReady);
            Assert.IsFalse(peerAEncryption.IsReady());
        }

        [Test]
        public void WithGarbage()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            byte[] initialPayload = new byte[1000];
            random.GetBytes(initialPayload);

            SocketPipe pipe = new SocketPipe();
            byte[] InfoHash = new byte[20];
            random.GetBytes(InfoHash);

            PeerAEncryption peerAEncryption = new PeerAEncryption(InfoHash, EncryptionType.RC4Full);
            peerAEncryption.SetPeerConnectionID(null);
            peerAEncryption.onEncryptorReady += new EncryptorReadyHandler(peerAEncryption_onEncryptorReady);
            peerAEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerAEncryption_onEncryptorIOError);
            peerAEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerAEncryption_onEncryptorEncryptionError);

            peerAEncryption.AddInitialData(initialPayload, 0, initialPayload.Length);
            peerAEncryption.Start(pipe.ClientSocket);

            pipe.ServerSocket.Blocking = true;

            pipe.ServerSocket.Send(initialPayload);

            System.Threading.Thread.Sleep(1000);

            Assert.IsFalse(onIOError);
            Assert.IsTrue(onEncryptionError);
            Assert.IsFalse(onReady);
            Assert.IsFalse(peerAEncryption.IsReady());
        }

        [Test]
        public void WithoutPayload()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            SocketPipe pipe = new SocketPipe();
            byte[] InfoHash = new byte[20];
            random.GetBytes(InfoHash);

            PeerAEncryption peerAEncryption = new PeerAEncryption(InfoHash, EncryptionType.RC4Full);
            peerAEncryption.SetPeerConnectionID(null);
            peerAEncryption.onEncryptorReady += new EncryptorReadyHandler(peerAEncryption_onEncryptorReady);
            peerAEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerAEncryption_onEncryptorIOError);
            peerAEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerAEncryption_onEncryptorEncryptionError);

            peerAEncryption.Start(pipe.ClientSocket);

            pipe.ServerSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ServerSocket.Send(Y);
            pipe.ServerSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ServerSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);

            byte[] req1 = sha1.ComputeHash(req1S);

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 532; i++)
            {
                pipe.ServerSocket.Receive(byteBuffer);
                if (byteBuffer[0] == req1[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == req1.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 20);

            byte[] req2S = new byte[4 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("req2"), req2S, 4);
            Array.Copy(InfoHash, 0, req2S, 4, 20);

            byte[] req3S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req3"), req3S, 4);
            Array.Copy(S, 0, req3S, 4, 96);

            byte[] req2 = sha1.ComputeHash(req2S);
            byte[] req3 = sha1.ComputeHash(req3S);

            for (int i = 0; i < 20; i++)
            {
                req2[i] ^= req3[i];
            }

            byte[] req2r = new byte[20];
            pipe.ServerSocket.Receive(req2r);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(req2, req2r));

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = new byte[8];
            pipe.ServerSocket.Receive(vc);
            decryptor.InPlaceCrypt(vc, 0, 8);

            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(vc[i] == 0);
            }

            byte[] cp = new byte[4];
            pipe.ServerSocket.Receive(cp);
            decryptor.InPlaceCrypt(cp, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cp[i] == 0);
            }

            Assert.IsTrue(cp[3] == 2);

            byte[] lenPadC = new byte[2];
            pipe.ServerSocket.Receive(lenPadC);
            decryptor.InPlaceCrypt(lenPadC, 0, 2);

            byte[] padC = new byte[(lenPadC[0] << 8) + lenPadC[1]];
            pipe.ServerSocket.Receive(padC);
            decryptor.InPlaceCrypt(padC, 0, padC.Length);

            for (int i = 0; i < padC.Length; i++)
            {
                Assert.IsTrue(padC[i] == 0);
            }

            byte[] lenIA = new byte[2];
            pipe.ServerSocket.Receive(lenIA);
            decryptor.InPlaceCrypt(lenIA, 0, 2);

            byte[] initialPayloadr = new byte[(lenIA[0] << 8) + lenIA[1]];

            Assert.IsTrue(initialPayloadr.Length == 0);

            encryptor.InPlaceCrypt(vc, 0, 8);
            encryptor.InPlaceCrypt(cp, 0, 4);
            encryptor.InPlaceCrypt(lenPadC, 0, 2);
            encryptor.InPlaceCrypt(padC, 0, padC.Length);

            pipe.ServerSocket.Send(vc);
            pipe.ServerSocket.Send(cp);
            pipe.ServerSocket.Send(lenPadC);
            pipe.ServerSocket.Send(padC);

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsFalse(onEncryptionError);
            Assert.IsTrue(onReady);
            Assert.IsTrue(peerAEncryption.IsReady());

            byte[] payload = new byte[random2.Next(1, 1000)];
            random.GetBytes(payload);

            byte[] payloadr = new byte[payload.Length];
            payload.CopyTo(payloadr, 0);

            peerAEncryption.Encrypt(payloadr, 0, payloadr.Length);
            pipe.ClientSocket.Send(payloadr);
            pipe.ServerSocket.Receive(payloadr);
            decryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

            encryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            pipe.ServerSocket.Send(payloadr);
            pipe.ClientSocket.Receive(payloadr);
            peerAEncryption.Decrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));
        }

        void peerAEncryption_onEncryptorEncryptionError(MonoTorrent.Client.PeerConnectionID id)
        {
            Assert.IsTrue(id == null);
            onEncryptionError = true;
        }

        void peerAEncryption_onEncryptorIOError(MonoTorrent.Client.PeerConnectionID id)
        {
            Assert.IsTrue(id == null);
            onIOError = true;
        }

        void peerAEncryption_onEncryptorReady(MonoTorrent.Client.PeerConnectionID id)
        {
            Assert.IsTrue(id == null);
            onReady = true;
        }
    }
}
