using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using MonoTorrent.Client.Encryption;
using System.Security.Cryptography;
using NUnit.Framework;
using MonoTorrent.Common;

namespace MonoTorrent.Tests.Encryption.Tests
{
    [TestFixture]
    public class PeerBEncryptionTest
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
            

            byte[][] infohashes = new byte[random2.Next(1, 10)][];

            for (int i = 0; i < infohashes.Length; i++)
            {
                infohashes[i] = new byte[20];
                random.GetBytes(infohashes[i]);
            }

            byte[] InfoHash = infohashes[random2.Next(0, infohashes.Length - 1)];

            PeerBEncryption peerBEncryption = new PeerBEncryption(infohashes, EncryptionType.RC4Full);
            peerBEncryption.SetPeerConnectionID(null);
            peerBEncryption.onEncryptorReady += new EncryptorReadyHandler(peerBEncryption_onEncryptorReady);
            peerBEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerBEncryption_onEncryptorIOError);
            peerBEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerBEncryption_onEncryptorEncryptionError);

            peerBEncryption.Start(pipe.ServerSocket);

            pipe.ClientSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ClientSocket.Send(Y);
            pipe.ClientSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ClientSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);
            byte[] req1 = sha1.ComputeHash(req1S);

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

            pipe.ClientSocket.Send(req1);
            pipe.ClientSocket.Send(req2);

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = encryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            pipe.ClientSocket.Send(vc);
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { 0, 0, 0, 3 }));

            byte[] padC = new byte[random2.Next(0, 512)];
            for (int i = 0; i < padC.Length; i++)
                padC[i] = 0;
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((padC.Length >> 8) & 0xff), (byte)(padC.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(padC));

            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((initialPayload.Length >> 8) & 0xff), (byte)(initialPayload.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(initialPayload));

            vc = decryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 524; i++)
            {
                pipe.ClientSocket.Receive(byteBuffer);
                if (byteBuffer[0] == vc[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == vc.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 8);

            byte[] cs = new byte[4];
            pipe.ClientSocket.Receive(cs);
            decryptor.InPlaceCrypt(cs, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cs[i] == 0);
            }

            Assert.IsTrue(cs[3] == 2);

            byte[] lenPadD = new byte[2];
            pipe.ClientSocket.Receive(lenPadD);
            decryptor.InPlaceCrypt(lenPadD, 0, 2);

            byte[] padD = new byte[(lenPadD[0] << 8) + lenPadD[1]];
            pipe.ClientSocket.Receive(padD);
            decryptor.InPlaceCrypt(padD, 0, padD.Length);

            for (int i = 0; i < padD.Length; i++)
            {
                Assert.IsTrue(padD[i] == 0);
            }

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsFalse(onEncryptionError);
            Assert.IsTrue(onReady);
            Assert.IsTrue(peerBEncryption.IsReady());

            Assert.IsTrue(peerBEncryption.IsInitialDataAvailable());

            byte[] initialPayloadr = new byte[initialPayload.Length];
            peerBEncryption.GetInitialData(initialPayloadr, 0, initialPayloadr.Length);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(initialPayload, initialPayloadr));

            byte[] payload = new byte[random2.Next(1, 1000)];
            random.GetBytes(payload);

            byte[] payloadr = new byte[payload.Length];
            payload.CopyTo(payloadr, 0);

            peerBEncryption.Encrypt(payloadr, 0, payloadr.Length);
            pipe.ServerSocket.Send(payloadr);
            pipe.ClientSocket.Receive(payloadr);
            decryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

            encryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            pipe.ClientSocket.Send(payloadr);
            pipe.ServerSocket.Receive(payloadr);
            peerBEncryption.Decrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));
        }

        [Test]
        public void WithoutPayLoad()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            SocketPipe pipe = new SocketPipe();


            byte[][] infohashes = new byte[random2.Next(1, 10)][];

            for (int i = 0; i < infohashes.Length; i++)
            {
                infohashes[i] = new byte[20];
                random.GetBytes(infohashes[i]);
            }

            byte[] InfoHash = infohashes[random2.Next(0, infohashes.Length - 1)];

            PeerBEncryption peerBEncryption = new PeerBEncryption(infohashes, EncryptionType.RC4Full);
            peerBEncryption.SetPeerConnectionID(null);
            peerBEncryption.onEncryptorReady += new EncryptorReadyHandler(peerBEncryption_onEncryptorReady);
            peerBEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerBEncryption_onEncryptorIOError);
            peerBEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerBEncryption_onEncryptorEncryptionError);

            peerBEncryption.Start(pipe.ServerSocket);

            pipe.ClientSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ClientSocket.Send(Y);
            pipe.ClientSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ClientSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);
            byte[] req1 = sha1.ComputeHash(req1S);

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

            pipe.ClientSocket.Send(req1);
            pipe.ClientSocket.Send(req2);

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = encryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            pipe.ClientSocket.Send(vc);
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { 0, 0, 0, 3 }));

            byte[] padC = new byte[random2.Next(0, 512)];
            for (int i = 0; i < padC.Length; i++)
                padC[i] = 0;
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((padC.Length >> 8) & 0xff), (byte)(padC.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(padC));

            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { 0, 0 }));

            vc = decryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 524; i++)
            {
                pipe.ClientSocket.Receive(byteBuffer);
                if (byteBuffer[0] == vc[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == vc.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 8);

            byte[] cs = new byte[4];
            pipe.ClientSocket.Receive(cs);
            decryptor.InPlaceCrypt(cs, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cs[i] == 0);
            }

            Assert.IsTrue(cs[3] == 2);

            byte[] lenPadD = new byte[2];
            pipe.ClientSocket.Receive(lenPadD);
            decryptor.InPlaceCrypt(lenPadD, 0, 2);

            byte[] padD = new byte[(lenPadD[0] << 8) + lenPadD[1]];
            pipe.ClientSocket.Receive(padD);
            decryptor.InPlaceCrypt(padD, 0, padD.Length);

            for (int i = 0; i < padD.Length; i++)
            {
                Assert.IsTrue(padD[i] == 0);
            }

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsFalse(onEncryptionError);
            Assert.IsTrue(onReady);
            Assert.IsTrue(peerBEncryption.IsReady());

            Assert.IsFalse(peerBEncryption.IsInitialDataAvailable());

            byte[] payload = new byte[random2.Next(1, 1000)];
            random.GetBytes(payload);

            byte[] payloadr = new byte[payload.Length];
            payload.CopyTo(payloadr, 0);

            peerBEncryption.Encrypt(payloadr, 0, payloadr.Length);
            pipe.ServerSocket.Send(payloadr);
            pipe.ClientSocket.Receive(payloadr);
            decryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

            encryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            pipe.ClientSocket.Send(payloadr);
            pipe.ServerSocket.Receive(payloadr);
            peerBEncryption.Decrypt(payloadr, 0, payloadr.Length);
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


            byte[][] infohashes = new byte[random2.Next(1, 10)][];

            for (int i = 0; i < infohashes.Length; i++)
            {
                infohashes[i] = new byte[20];
                random.GetBytes(infohashes[i]);
            }

            byte[] InfoHash = infohashes[random2.Next(0, infohashes.Length - 1)];

            PeerBEncryption peerBEncryption = new PeerBEncryption(infohashes, EncryptionType.RC4Header);
            peerBEncryption.SetPeerConnectionID(null);
            peerBEncryption.onEncryptorReady += new EncryptorReadyHandler(peerBEncryption_onEncryptorReady);
            peerBEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerBEncryption_onEncryptorIOError);
            peerBEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerBEncryption_onEncryptorEncryptionError);

            peerBEncryption.Start(pipe.ServerSocket);

            pipe.ClientSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ClientSocket.Send(Y);
            pipe.ClientSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ClientSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);
            byte[] req1 = sha1.ComputeHash(req1S);

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

            pipe.ClientSocket.Send(req1);
            pipe.ClientSocket.Send(req2);

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = encryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            pipe.ClientSocket.Send(vc);
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { 0, 0, 0, 1 }));

            byte[] padC = new byte[random2.Next(0, 512)];
            for (int i = 0; i < padC.Length; i++)
                padC[i] = 0;
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((padC.Length >> 8) & 0xff), (byte)(padC.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(padC));

            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((initialPayload.Length >> 8) & 0xff), (byte)(initialPayload.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(initialPayload));

            vc = decryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 524; i++)
            {
                pipe.ClientSocket.Receive(byteBuffer);
                if (byteBuffer[0] == vc[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == vc.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 8);

            byte[] cs = new byte[4];
            pipe.ClientSocket.Receive(cs);
            decryptor.InPlaceCrypt(cs, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cs[i] == 0);
            }

            Assert.IsTrue(cs[3] == 1);

            byte[] lenPadD = new byte[2];
            pipe.ClientSocket.Receive(lenPadD);
            decryptor.InPlaceCrypt(lenPadD, 0, 2);

            byte[] padD = new byte[(lenPadD[0] << 8) + lenPadD[1]];
            pipe.ClientSocket.Receive(padD);
            decryptor.InPlaceCrypt(padD, 0, padD.Length);

            for (int i = 0; i < padD.Length; i++)
            {
                Assert.IsTrue(padD[i] == 0);
            }

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsFalse(onEncryptionError);
            Assert.IsTrue(onReady);
            Assert.IsTrue(peerBEncryption.IsReady());

            Assert.IsTrue(peerBEncryption.IsInitialDataAvailable());

            byte[] initialPayloadr = new byte[initialPayload.Length];
            peerBEncryption.GetInitialData(initialPayloadr, 0, initialPayloadr.Length);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(initialPayload, initialPayloadr));

            byte[] payload = new byte[random2.Next(1, 1000)];
            random.GetBytes(payload);

            byte[] payloadr = new byte[payload.Length];
            payload.CopyTo(payloadr, 0);

            peerBEncryption.Encrypt(payloadr, 0, payloadr.Length);
            pipe.ServerSocket.Send(payloadr);
            pipe.ClientSocket.Receive(payloadr);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

            pipe.ClientSocket.Send(payloadr);
            pipe.ServerSocket.Receive(payloadr);
            peerBEncryption.Decrypt(payloadr, 0, payloadr.Length);
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


            byte[][] infohashes = new byte[random2.Next(1, 10)][];

            for (int i = 0; i < infohashes.Length; i++)
            {
                infohashes[i] = new byte[20];
                random.GetBytes(infohashes[i]);
            }

            byte[] InfoHash = infohashes[random2.Next(0, infohashes.Length - 1)];

            PeerBEncryption peerBEncryption = new PeerBEncryption(infohashes, EncryptionType.RC4Full);
            peerBEncryption.SetPeerConnectionID(null);
            peerBEncryption.onEncryptorReady += new EncryptorReadyHandler(peerBEncryption_onEncryptorReady);
            peerBEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerBEncryption_onEncryptorIOError);
            peerBEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerBEncryption_onEncryptorEncryptionError);

            peerBEncryption.Start(pipe.ServerSocket);

            pipe.ClientSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ClientSocket.Send(Y);
            pipe.ClientSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ClientSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);
            byte[] req1 = sha1.ComputeHash(req1S);

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

            pipe.ClientSocket.Send(req1);
            pipe.ClientSocket.Send(req2);

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = encryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            pipe.ClientSocket.Send(vc);
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { 0, 0, 0, 1 }));

            byte[] padC = new byte[random2.Next(0, 512)];
            for (int i = 0; i < padC.Length; i++)
                padC[i] = 0;
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((padC.Length >> 8) & 0xff), (byte)(padC.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(padC));

            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((initialPayload.Length >> 8) & 0xff), (byte)(initialPayload.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(initialPayload));

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsTrue(onEncryptionError);
            Assert.IsFalse(onReady);
            Assert.IsFalse(peerBEncryption.IsReady());
        }

        [Test]
        public void WithUnrecognizedSKEY()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            byte[] initialPayload = new byte[random2.Next(1, 1000)];
            random.GetBytes(initialPayload);

            SocketPipe pipe = new SocketPipe();


            byte[][] infohashes = new byte[random2.Next(1, 10)][];

            for (int i = 0; i < infohashes.Length; i++)
            {
                infohashes[i] = new byte[20];
                random.GetBytes(infohashes[i]);
            }

            byte[] InfoHash = new byte[20];
            random.GetBytes(InfoHash);

            PeerBEncryption peerBEncryption = new PeerBEncryption(infohashes, EncryptionType.RC4Full);
            peerBEncryption.SetPeerConnectionID(null);
            peerBEncryption.onEncryptorReady += new EncryptorReadyHandler(peerBEncryption_onEncryptorReady);
            peerBEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerBEncryption_onEncryptorIOError);
            peerBEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerBEncryption_onEncryptorEncryptionError);

            peerBEncryption.Start(pipe.ServerSocket);

            pipe.ClientSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ClientSocket.Send(Y);
            pipe.ClientSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ClientSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);
            byte[] req1 = sha1.ComputeHash(req1S);

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

            pipe.ClientSocket.Send(req1);
            pipe.ClientSocket.Send(req2);

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = encryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            pipe.ClientSocket.Send(vc);
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { 0, 0, 0, 3 }));

            byte[] padC = new byte[random2.Next(0, 512)];
            for (int i = 0; i < padC.Length; i++)
                padC[i] = 0;
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((padC.Length >> 8) & 0xff), (byte)(padC.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(padC));

            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((initialPayload.Length >> 8) & 0xff), (byte)(initialPayload.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(initialPayload));

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsTrue(onEncryptionError);
            Assert.IsFalse(onReady);
            Assert.IsFalse(peerBEncryption.IsReady());
        }

        [Test]
        public void WithGarbage()
        {
            onEncryptionError = false;
            onIOError = false;
            onReady = false;

            SocketPipe pipe = new SocketPipe();

            byte[][] infohashes = new byte[random2.Next(1, 10)][];

            for (int i = 0; i < infohashes.Length; i++)
            {
                infohashes[i] = new byte[20];
                random.GetBytes(infohashes[i]);
            }

            PeerBEncryption peerBEncryption = new PeerBEncryption(infohashes, EncryptionType.RC4Full);
            peerBEncryption.SetPeerConnectionID(null);
            peerBEncryption.onEncryptorReady += new EncryptorReadyHandler(peerBEncryption_onEncryptorReady);
            peerBEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerBEncryption_onEncryptorIOError);
            peerBEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerBEncryption_onEncryptorEncryptionError);

            peerBEncryption.Start(pipe.ServerSocket);

            pipe.ClientSocket.Blocking = true;

            byte[] garbage = new byte[1000];
            random.GetBytes(garbage);

            pipe.ClientSocket.Send(garbage);

            System.Threading.Thread.Sleep(1000);

            Assert.IsFalse(onIOError);
            Assert.IsTrue(onEncryptionError);
            Assert.IsFalse(onReady);
            Assert.IsFalse(peerBEncryption.IsReady());
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


            byte[][] infohashes = new byte[random2.Next(1, 10)][];

            for (int i = 0; i < infohashes.Length; i++)
            {
                infohashes[i] = new byte[20];
                random.GetBytes(infohashes[i]);
            }

            byte[] InfoHash = infohashes[random2.Next(0, infohashes.Length - 1)];

            PeerBEncryption peerBEncryption = new PeerBEncryption(infohashes, EncryptionType.RC4Full);
            peerBEncryption.SetPeerConnectionID(null);
            peerBEncryption.onEncryptorReady += new EncryptorReadyHandler(peerBEncryption_onEncryptorReady);
            peerBEncryption.onEncryptorIOError += new EncryptorIOErrorHandler(peerBEncryption_onEncryptorIOError);
            peerBEncryption.onEncryptorEncryptionError += new EncryptorEncryptionErrorHandler(peerBEncryption_onEncryptorEncryptionError);

            peerBEncryption.Start(pipe.ServerSocket);

            pipe.ClientSocket.Blocking = true;

            byte[] X = new byte[96];

            for (int i = 0; i < 76; i++)
                X[i] = 0;

            byte[] Xkey = new byte[20];
            random.GetNonZeroBytes(Xkey);
            Xkey.CopyTo(X, 76);

            byte[] pad = new byte[random2.Next(0, 512)];
            random.GetBytes(pad);

            byte[] Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
            pipe.ClientSocket.Send(Y);
            pipe.ClientSocket.Send(pad);

            byte[] otherY = new byte[96];
            pipe.ClientSocket.Receive(otherY);

            byte[] S = ModuloCalculator.Calculate(otherY, X);

            Console.WriteLine(ModuloCalculator.GetString(S));

            byte[] req1S = new byte[4 + 96];
            Array.Copy(Encoding.ASCII.GetBytes("req1"), req1S, 4);
            Array.Copy(S, 0, req1S, 4, 96);
            byte[] req1 = sha1.ComputeHash(req1S);

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

            pipe.ClientSocket.Send(req1);
            pipe.ClientSocket.Send(req2);

            byte[] keyS = new byte[4 + 96 + 20];
            Array.Copy(Encoding.ASCII.GetBytes("keyB"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 decryptor = new RC4(sha1.ComputeHash(keyS));

            Array.Copy(Encoding.ASCII.GetBytes("keyA"), keyS, 4);
            Array.Copy(S, 0, keyS, 4, 96);
            Array.Copy(InfoHash, 0, keyS, 100, 20);
            RC4 encryptor = new RC4(sha1.ComputeHash(keyS));

            byte[] vc = encryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            pipe.ClientSocket.Send(vc);
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { 0, 1, 0, 3 }));

            byte[] padC = new byte[random2.Next(0, 512)];
            for (int i = 0; i < padC.Length; i++)
                padC[i] = 0;
            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((padC.Length >> 8) & 0xff), (byte)(padC.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(padC));

            pipe.ClientSocket.Send(encryptor.DoCrypt(new byte[] { (byte)((initialPayload.Length >> 8) & 0xff), (byte)(initialPayload.Length & 0xff) }));
            pipe.ClientSocket.Send(encryptor.DoCrypt(initialPayload));

            vc = decryptor.DoCrypt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });

            int syncPoint = 0;
            byte[] byteBuffer = new byte[1];
            for (int i = 0; i < 524; i++)
            {
                pipe.ClientSocket.Receive(byteBuffer);
                if (byteBuffer[0] == vc[syncPoint])
                    syncPoint++;
                else
                    syncPoint = 0;

                if (syncPoint == vc.Length)
                    break;
            }

            Assert.IsTrue(syncPoint == 8);

            byte[] cs = new byte[4];
            pipe.ClientSocket.Receive(cs);
            decryptor.InPlaceCrypt(cs, 0, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(cs[i] == 0);
            }

            Assert.IsTrue(cs[3] == 2);

            byte[] lenPadD = new byte[2];
            pipe.ClientSocket.Receive(lenPadD);
            decryptor.InPlaceCrypt(lenPadD, 0, 2);

            byte[] padD = new byte[(lenPadD[0] << 8) + lenPadD[1]];
            pipe.ClientSocket.Receive(padD);
            decryptor.InPlaceCrypt(padD, 0, padD.Length);

            for (int i = 0; i < padD.Length; i++)
            {
                Assert.IsTrue(padD[i] == 0);
            }

            System.Threading.Thread.Sleep(100);

            Assert.IsFalse(onIOError);
            Assert.IsFalse(onEncryptionError);
            Assert.IsTrue(onReady);
            Assert.IsTrue(peerBEncryption.IsReady());

            Assert.IsTrue(peerBEncryption.IsInitialDataAvailable());

            byte[] initialPayloadr = new byte[initialPayload.Length];
            peerBEncryption.GetInitialData(initialPayloadr, 0, initialPayloadr.Length);

            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(initialPayload, initialPayloadr));

            byte[] payload = new byte[random2.Next(1, 1000)];
            random.GetBytes(payload);

            byte[] payloadr = new byte[payload.Length];
            payload.CopyTo(payloadr, 0);

            peerBEncryption.Encrypt(payloadr, 0, payloadr.Length);
            pipe.ServerSocket.Send(payloadr);
            pipe.ClientSocket.Receive(payloadr);
            decryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));

            encryptor.InPlaceCrypt(payloadr, 0, payloadr.Length);
            pipe.ClientSocket.Send(payloadr);
            pipe.ServerSocket.Receive(payloadr);
            peerBEncryption.Decrypt(payloadr, 0, payloadr.Length);
            Assert.IsTrue(MonoTorrent.Common.ToolBox.ByteMatch(payload, payloadr));
        }

        void peerBEncryption_onEncryptorEncryptionError(MonoTorrent.Client.PeerConnectionID id)
        {
            Assert.IsTrue(id == null);
            onEncryptionError = true;
        }

        void peerBEncryption_onEncryptorIOError(MonoTorrent.Client.PeerConnectionID id)
        {
            Assert.IsTrue(id == null);
            onIOError = true;
        }

        void peerBEncryption_onEncryptorReady(MonoTorrent.Client.PeerConnectionID id)
        {
            Assert.IsTrue(id == null);
            onReady = true;
        }
    }
}
