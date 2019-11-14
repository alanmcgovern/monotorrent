//
// EncryptedSocket.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;
using ReusableTasks;

namespace MonoTorrent.Client.Encryption
{
    sealed class EncryptionException : TorrentException
    {
        public EncryptionException()
        {

        }

        public EncryptionException(string message)
            : base(message)
        {

        }

        public EncryptionException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }

    /// <summary>
    /// The class that handles.Message Stream Encryption for a connection
    /// </summary>
    abstract class EncryptedSocket : IEncryptor
    {
        protected static readonly byte[] Req1Bytes = { (byte)'r', (byte)'e', (byte)'q', (byte)'1' };
        protected static readonly byte[] Req2Bytes = { (byte)'r', (byte)'e', (byte)'q', (byte)'2' };
        protected static readonly byte[] Req3Bytes = { (byte)'r', (byte)'e', (byte)'q', (byte)'3' };

        protected static readonly byte[] KeyABytes = { (byte)'k', (byte)'e', (byte)'y', (byte)'A' };
        protected static readonly byte[] KeyBBytes = { (byte)'k', (byte)'e', (byte)'y', (byte)'B' };

        // Cryptors for the data transmission
        public IEncryption Decryptor { get; private set; }
        public IEncryption Encryptor { get; private set; }

        #region Private members

        readonly RandomNumberGenerator random;
        readonly SHA1 hasher;

        // Cryptors for the handshaking
        private RC4 encryptor;
        private RC4 decryptor;

        private EncryptionTypes allowedEncryption;

        readonly byte[] X; // A 160 bit random integer
        readonly byte[] Y; // 2^X mod P
        
        protected IConnection2 socket;

        // Data to be passed to initial ReceiveMessage requests
        private byte[] initialBuffer;
        private int initialBufferOffset;
        private int initialBufferCount;

        // State information to be checked against abort conditions
        private int bytesReceived;

        #endregion

        #region Protected members
        protected byte[] S;
        protected InfoHash SKEY;

        protected byte[] VerificationConstant = new byte[8];

        protected byte[] CryptoProvide = { 0x00, 0x00, 0x00, 0x03 };


        protected byte[] CryptoSelect;

        #endregion

        protected EncryptedSocket(EncryptionTypes allowedEncryption)
        {
            random = RandomNumberGenerator.Create ();
            hasher = HashAlgoFactory.Create<SHA1>();

            X = new byte[20];
            random.GetBytes(X);
            Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);

            SetMinCryptoAllowed(allowedEncryption);
        }

        #region Interface implementation

        /// <summary>
        /// Begins the message stream encryption handshaking process
        /// </summary>
        /// <param name="socket">The socket to perform handshaking with</param>
        public virtual async ReusableTask HandshakeAsync(IConnection2 socket)
        {
            this.socket = socket ?? throw new ArgumentNullException(nameof (socket));

            // Either "1 A->B: Diffie Hellman Ya, PadA" or "2 B->A: Diffie Hellman Yb, PadB"
            // These two steps will be done simultaneously to save time due to latency
            var first = SendY();
            var second = ReceiveY();
            try
            {
                await first;
                await second;
            } catch (Exception ex) {
                socket.Dispose();
                throw new EncryptionException ("Encrypted handshake failed", ex);
            }
        }

        /// <summary>
        /// Begins the message stream encryption handshaking process, beginning with some data
        /// already received from the socket.
        /// </summary>
        /// <param name="socket">The socket to perform handshaking with</param>
        /// <param name="initialBuffer">Buffer containing soome data already received from the socket</param>
        /// <param name="offset">Offset to begin reading in initialBuffer</param>
        /// <param name="count">Number of bytes to read from initialBuffer</param>
        public virtual async ReusableTask HandshakeAsync(IConnection2 socket, byte[] initialBuffer, int offset, int count)
        {
            this.initialBuffer = initialBuffer;
            this.initialBufferOffset = offset;
            this.initialBufferCount = count;
            await HandshakeAsync(socket);
        }


        /// <summary>
        /// Encrypts some data (should only be called after onEncryptorReady)
        /// </summary>
        /// <param name="data">Buffer with the data to encrypt</param>
        /// <param name="offset">Offset to begin encryption</param>
        /// <param name="length">Number of bytes to encrypt</param>
        public void Encrypt(byte[] data, int offset, int length)
        {
            Encryptor.Encrypt(data, offset, data, offset, length);
        }

        /// <summary>
        /// Decrypts some data (should only be called after onEncryptorReady)
        /// </summary>
        /// <param name="data">Buffer with the data to decrypt</param>
        /// <param name="offset">Offset to begin decryption</param>
        /// <param name="length">Number of bytes to decrypt</param>
        public void Decrypt(byte[] data, int offset, int length)
        {
            Decryptor.Decrypt(data, offset, data, offset, length);
        }

        private int RandomNumber(int max)
        {
            byte[] b = new byte[4];
            random.GetBytes(b);
            uint val = BitConverter.ToUInt32(b, 0);
            return (int)(val % max);
        }
        #endregion

        #region Diffie-Hellman Key Exchange Functions

        /// <summary>
        /// Send Y to the remote client, with a random padding that is 0 to 512 bytes long
        /// (Either "1 A->B: Diffie Hellman Ya, PadA" or "2 B->A: Diffie Hellman Yb, PadB")
        /// </summary>
        protected async ReusableTask SendY()
        {
            var length = 96 + RandomNumber(512);
            var toSend = ClientEngine.BufferPool.Rent (length);
            Buffer.BlockCopy(Y, 0, toSend, 0, 96);
            random.GetBytes(toSend, 96, length - 96);
            try  {
                await NetworkIO.SendAsync(socket, toSend, 0, length, null, null, null).ConfigureAwait(false);
            } finally {
                ClientEngine.BufferPool.Return(toSend);
            }
        }

        /// <summary>
        /// Receive the first 768 bits of the transmission from the remote client, which is Y in the protocol
        /// (Either "1 A->B: Diffie Hellman Ya, PadA" or "2 B->A: Diffie Hellman Yb, PadB")
        /// </summary>
        protected async ReusableTask ReceiveY()
        {
            var otherY = new byte[96];
            await ReceiveMessage(otherY, 96);
            S = ModuloCalculator.Calculate (otherY, X);
            await doneReceiveY ();
        }

        protected abstract ReusableTask doneReceiveY ();

        #endregion

        #region Synchronization functions
        /// <summary>
        /// Read data from the socket until the byte string in syncData is read, or until syncStopPoint
        /// is reached (in that case, there is an EncryptionError).
        /// (Either "3 A->B: HASH('req1', S)" or "4 B->A: ENCRYPT(VC)")
        /// </summary>
        /// <param name="syncData">Buffer with the data to synchronize to</param>
        /// <param name="syncStopPoint">Maximum number of bytes (measured from the total received from the socket since connection) to read before giving up</param>
        protected async ReusableTask Synchronize(byte[] syncData, int syncStopPoint)
        {
            // The strategy here is to create a window the size of the data to synchronize and just refill that until its contents match syncData
            int filled = 0;
            var synchronizeWindow = ClientEngine.BufferPool.Rent(syncData.Length);

            while (bytesReceived < syncStopPoint)
            {
                int received = syncData.Length - filled;
                await NetworkIO.ReceiveAsync(socket, synchronizeWindow, filled, received, null, null, null).ConfigureAwait (false);

                bytesReceived += received;
                bool matched = true;
                for (int i = 0; i < syncData.Length && matched; i++)
                    matched &= syncData[i] == synchronizeWindow[i];

                if (matched) // the match started in the beginning of the window, so it must be a full match
                {
                    await doneSynchronize ().ConfigureAwait (false);
                    return;
                }
                else
                {
                    // See if the current window contains the first byte of the expected synchronize data
                    // No need to check synchronizeWindow[0] as otherwise we could loop forever receiving 0 bytes
                    int shift = -1;
                    for (int i = 1; i < syncData.Length && shift == -1; i++)
                        if (synchronizeWindow[i] == syncData[0])
                            shift = i;

                    // The current data is all useless, so read an entire new window of data
                    if (shift > 0)
                    {
                        filled = syncData.Length - shift;
                        // Shuffle everything left by 'shift' (the first good byte) and fill the rest of the window
                        Buffer.BlockCopy(synchronizeWindow, shift, synchronizeWindow, 0, syncData.Length - shift);
                    } else
                    {
                        // The start point we thought we had is actually garbage, so throw away all the data we have
                        filled = 0;
                    }
                }
            }
            throw new EncryptionException("Couldn't synchronise 1");
        }

        protected virtual ReusableTask doneSynchronize()
        {
            return ReusableTask.CompletedTask;
        }
        #endregion

        #region I/O Functions
        protected async ReusableTask ReceiveMessage(byte[] buffer, int length)
        {
            if (length == 0)
            {
                return;
            }
            if (initialBuffer != null)
            {
                int toCopy = Math.Min(initialBufferCount, length);
                Array.Copy(initialBuffer, initialBufferOffset, buffer, 0, toCopy);
                initialBufferOffset += toCopy;
                initialBufferCount -= toCopy;

                if (toCopy == initialBufferCount)
                {
                    initialBufferCount = 0;
                    initialBufferOffset = 0;
                    initialBuffer = Array.Empty<byte> ();
                }

                if (toCopy != length)
                {
                    await NetworkIO.ReceiveAsync(socket, buffer, toCopy, length - toCopy, null, null, null).ConfigureAwait (false);
                    bytesReceived += length - toCopy;
                }
            }
            else
            {
                await NetworkIO.ReceiveAsync(socket, buffer, 0, length, null, null, null).ConfigureAwait (false);
                bytesReceived += length;
            }
        }

        #endregion

        #region Cryptography Setup
        /// <summary>
        /// Instantiate the cryptors with the keys: Hash(encryptionSalt, S, SKEY) for the encryptor and
        /// Hash(encryptionSalt, S, SKEY) for the decryptor.
        /// (encryptionSalt should be "keyA" if you're A, "keyB" if you're B, and reverse for decryptionSalt)
        /// </summary>
        /// <param name="encryptionSalt">The salt to calculate the encryption key with</param>
        /// <param name="decryptionSalt">The salt to calculate the decryption key with</param>
        protected void CreateCryptors(byte[] encryptionSalt, byte[] decryptionSalt)
        {
            encryptor = new RC4(Hash(encryptionSalt, S, SKEY.Hash));
            decryptor = new RC4(Hash(decryptionSalt, S, SKEY.Hash));
        }

        /// <summary>
        /// Sets CryptoSelect and initializes the stream encryptor and decryptor based on the selected method.
        /// </summary>
        /// <param name="remoteCryptoBytes">The cryptographic methods supported/wanted by the remote client in CryptoProvide format. The highest order one available will be selected</param>
        /// <param name="replace">True if the existing Encryptor/Decryptor object should be replaced with a new instance</param>
        protected virtual int SelectCrypto(byte[] remoteCryptoBytes, bool replace)
        {
            CryptoSelect = new byte[remoteCryptoBytes.Length];

            // '2' corresponds to RC4Full
            if ((remoteCryptoBytes[3] & 2) == 2 && allowedEncryption.HasFlag (EncryptionTypes.RC4Full))
            {
                CryptoSelect[3] |= 2;
                if (replace)
                {
                    Encryptor = encryptor;
                    Decryptor = decryptor;
                }
                return 2;
            }
            
            // '1' corresponds to RC4Header
            if ((remoteCryptoBytes[3] & 1) == 1 && allowedEncryption.HasFlag (EncryptionTypes.RC4Header))
            {
                CryptoSelect[3] |= 1;
                if (replace)
                {
                    Encryptor = new RC4Header();
                    Decryptor = new RC4Header();
                }
                return 1;
            }

            throw new EncryptionException("No valid encryption method detected");
        }
        #endregion

        #region Utility Functions

        /// <summary>
        /// Concatenates several byte buffers
        /// </summary>
        /// <param name="data">Buffers to concatenate</param>
        /// <returns>Resulting concatenated buffer</returns>
        protected byte[] Combine(params byte[][] data)
        {
            int cursor = 0;
            int totalLength = 0;
            byte[] combined;

            foreach (byte[] datum in data)
                totalLength += datum.Length;

            combined = new byte[totalLength];

            for (int i = 0; i < data.Length; i++)
                cursor += Message.Write(combined, cursor, data[i]);

            return combined;
        }

        /// <summary>
        /// Hash some data with SHA1
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <param name="third"></param>
        /// <returns>20-byte hash</returns>
        protected byte[] Hash(byte[] first, byte[] second, byte[] third = null)
        {
            hasher.Initialize ();
            hasher.TransformBlock (first, 0, first.Length, first, 0);
            hasher.TransformBlock (second, 0, second.Length, second, 0);
            if (third != null)
                hasher.TransformBlock (third, 0, third.Length, third, 0);
            hasher.TransformFinalBlock (Array.Empty<byte> (), 0, 0);
            return hasher.Hash;
        }

        /// <summary>
        /// Converts a 2-byte big endian integer into an int (reverses operation of Len())
        /// </summary>
        /// <param name="data">2 byte buffer</param>
        /// <returns>int</returns>
        protected int DeLen(byte[] data)
        {
            return (int)(data[0] << 8) + data[1];
        }

        /// <summary>
        /// Returns a 2-byte buffer with the length of data
        /// </summary>
        protected byte[] Len(byte[] data)
        {
            byte[] lenBuffer = new byte[2];
            lenBuffer[0] = (byte)((data.Length >> 8) & 0xff);
            lenBuffer[1] = (byte)((data.Length) & 0xff);
            return lenBuffer;
        }

        /// <summary>
        /// Returns a 0 to 512 byte 0-filled pad.
        /// </summary>
        protected byte[] GeneratePad()
        {
            return new byte[RandomNumber(512)];
        }
        #endregion

        #region Miscellaneous

        protected byte[] DoEncrypt(byte[] data)
        {
            encryptor.Encrypt(data);
            return data;
        }

        /// <summary>
        /// Encrypts some data with the RC4 encryptor used in handshaking
        /// </summary>
        /// <param name="data">Buffer with the data to encrypt</param>
        /// <param name="offset">Offset to begin encryption</param>
        /// <param name="length">Number of bytes to encrypt</param>
        protected void DoEncrypt(byte[] data, int offset, int length)
        {
            encryptor.Encrypt(data, offset, data, offset, length);
        }

        /// <summary>
        /// Decrypts some data with the RC4 decryptor used in handshaking
        /// </summary>
        /// <param name="data">Buffer with the data to decrypt</param>
        /// <param name="offset">Offset to begin decryption</param>
        /// <param name="length">Number of bytes to decrypt</param>
        protected void DoDecrypt(byte[] data, int offset, int length)
        {
            decryptor.Decrypt(data, offset, data, offset, length);
        }

        protected void SetMinCryptoAllowed(EncryptionTypes allowedEncryption)
        {
            this.allowedEncryption = allowedEncryption;

            // EncryptionType is basically a bit position starting from the right.
            // This sets all bits in CryptoProvide 0 that is to the right of minCryptoAllowed.
            CryptoProvide[0] = CryptoProvide[1] = CryptoProvide[2] = CryptoProvide[3] = 0;

            if (allowedEncryption.HasFlag (EncryptionTypes.RC4Full))
                CryptoProvide[3] |= 1 << 1;

            if (allowedEncryption.HasFlag (EncryptionTypes.RC4Header))
                CryptoProvide[3] |= 1;
        }
        #endregion
    }
}
