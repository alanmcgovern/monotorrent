//
// EncryptedSocket.cs
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
using System.Security.Cryptography;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;


namespace MonoTorrent.Client.Encryption
{

    /// <summary>
    /// The class that handles.Message Stream Encryption for a connection
    /// </summary>
    public class EncryptedSocket : IEncryptor
    {
        #region Events

        /// <summary>
        /// Invoked when handshaking is complete and the Encrypt() and Decrypt() methods are ready to be used
        /// </summary>
        public event EncryptorReadyHandler onEncryptorReady;

        /// <summary>
        /// Invoked when a socket error has occured
        /// </summary>
        public event EncryptorIOErrorHandler onEncryptorIOError;

        /// <summary>
        /// Invoked when encryption handshaking could not be completed due to something other than a socket error
        /// </summary>
        public event EncryptorEncryptionErrorHandler onEncryptorEncryptionError;
        #endregion

        #region Private members
        private bool isReady = false;

        private RandomNumberGenerator random;
        private SHA1 hasher;

        // Cryptors for the handshaking
        private RC4 encryptor = null;
        private RC4 decryptor = null;

        // Cryptors for the data transmission
        private IEncryption streamEncryptor;
        private IEncryption streamDecryptor;

        private EncryptionType minCryptoAllowed;

        private byte[] X; // A 160 bit random integer
        private byte[] Y; // 2^X mod P
        private byte[] OtherY = null;
        
        private Socket socket;

        // Data to be passed to initial ReceiveMessage requests
        private byte[] initialBuffer;
        private int initialBufferOffset;
        private int initialBufferCount;

        // State information to be checked against abort conditions
        private DateTime lastActivity;
        private int bytesReceived;

        // Callbacks
        private AsyncCallback doneSendCallback;
        private AsyncCallback doneReceiveCallback;
        private AsyncCallback doneReceiveYCallback;
        private AsyncCallback doneSynchronizeCallback;
        private AsyncCallback fillSynchronizeBytesCallback;

        // State information for synchronization
        private byte[] synchronizeData = null;
        private byte[] synchronizeWindow = null;
        private int syncStopPoint;
        #endregion

        #region Protected members
        protected byte[] S = null;
        protected byte[] SKEY = null;

        protected byte[] PadC = null;
        protected byte[] PadD = null;

        protected static byte[] VerificationConstant = new byte[] {
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        protected byte[] CryptoProvide = new byte[] {
            0x00, 0x00, 0x00, 0x03
        };

        protected byte[] InitialPayload;
        protected byte[] RemoteInitialPayload;

        protected byte[] CryptoSelect;

        protected PeerConnectionID id;
        #endregion

        public EncryptedSocket(EncryptionType minCryptoAllowed)
        {
            random = RandomNumberGenerator.Create();
            hasher = new SHA1Managed();

            GenerateX();
            GenerateY();

            InitialPayload = new byte[0];
            RemoteInitialPayload = new byte[0];

            doneSendCallback = new AsyncCallback(doneSend);
            doneReceiveCallback = new AsyncCallback(doneReceive);
            doneReceiveYCallback = new AsyncCallback(doneReceiveY);
            doneSynchronizeCallback = new AsyncCallback(doneSynchronize);
            fillSynchronizeBytesCallback = new AsyncCallback(fillSynchronizeBytes);

            lastActivity = DateTime.Now;
            bytesReceived = 0;

            initialBuffer = null;
            initialBufferOffset = 0;
            initialBufferCount = 0;

            SetMinCryptoAllowed(minCryptoAllowed);
        }

        #region Interface implementation

        /// <summary>
        /// Begins the message stream encryption handshaking process
        /// </summary>
        /// <param name="socket">The socket to perform handshaking with</param>
        public virtual void Start(Socket socket)
        {
            this.socket = socket;

            // Either "1 A->B: Diffie Hellman Ya, PadA" or "2 B->A: Diffie Hellman Yb, PadB"
            // These two steps will be done simultaneously to save time due to latency
            SendY();
            ReceiveY();
        }

        /// <summary>
        /// Begins the message stream encryption handshaking process, beginning with some data
        /// already received from the socket.
        /// </summary>
        /// <param name="socket">The socket to perform handshaking with</param>
        /// <param name="initialBuffer">Buffer containing soome data already received from the socket</param>
        /// <param name="offset">Offset to begin reading in initialBuffer</param>
        /// <param name="count">Number of bytes to read from initialBuffer</param>
        public virtual void Start(Socket socket, byte[] initialBuffer, int offset, int count)
        {
            this.initialBuffer = initialBuffer;
            this.initialBufferOffset = offset;
            this.initialBufferCount = count;
            Start(socket);
        }

        /// <summary>
        /// Returns true if the remote client has transmitted some initial payload data
        /// </summary>
        public bool IsInitialDataAvailable()
        {
            return RemoteInitialPayload.Length > 0;
        }

        /// <summary>
        /// Copies the payload initial data transferred from the remote client into a buffer
        /// </summary>
        /// <param name="buffer">Buffer to write the initial data to</param>
        /// <param name="offset">Offset to begin writing in buffer</param>
        /// <param name="count">Maximum number of bytes to write in buffer</param>
        /// <returns>Number of bytes written to buffer</returns>
        public int GetInitialData(byte[] buffer, int offset, int count)
        {
            int toCopy;

            if (count > RemoteInitialPayload.Length)
                toCopy = RemoteInitialPayload.Length;
            else
                toCopy = count;

            Array.Copy(RemoteInitialPayload, 0, buffer, offset, toCopy);

            byte[] newRemoteInitialPayload = new byte[RemoteInitialPayload.Length - toCopy];
            Array.Copy(RemoteInitialPayload, toCopy, newRemoteInitialPayload, 0, RemoteInitialPayload.Length - toCopy);
            RemoteInitialPayload = newRemoteInitialPayload;

            return toCopy;
        }

        /// <summary>
        /// Sets the PeerConnectionID associated with this connection
        /// </summary>
        /// <param name="id">The PeerConnectionID associated with this connection</param>
        public void SetPeerConnectionID(PeerConnectionID id)
        {
            this.id = id;
        }

        /// <summary>
        /// Encrypts some data (should only be called after onEncryptorReady)
        /// </summary>
        /// <param name="buffer">Buffer with the data to encrypt</param>
        /// <param name="offset">Offset to begin encryption</param>
        /// <param name="count">Number of bytes to encrypt</param>
        public void Encrypt(byte[] data, int offset, int length)
        {
            streamEncryptor.InPlaceCrypt(data, offset, length);
        }

        /// <summary>
        /// Decrypts some data (should only be called after onEncryptorReady)
        /// </summary>
        /// <param name="buffer">Buffer with the data to decrypt</param>
        /// <param name="offset">Offset to begin decryption</param>
        /// <param name="count">Number of bytes to decrypt</param>
        public void Decrypt(byte[] data, int offset, int length)
        {
            streamDecryptor.InPlaceCrypt(data, offset, length);
        }

        /// <summary>
        /// Specifies initial payload data to send to the remote client during handshaking
        /// </summary>
        /// <param name="buffer">Buffer with the initial payload data</param>
        /// <param name="offset">Offset to begin reading from</param>
        /// <param name="count">Number of bytes to read</param>
        public void AddInitialData(byte[] buffer, int offset, int count)
        {
            Logger.Log(id, "Adding " + count + " bytes to " + InitialPayload.Length + " byte initial payload for a total of ");
            
            byte[] newInitialPayload = new byte[InitialPayload.Length + count];
            Array.Copy(InitialPayload, newInitialPayload, InitialPayload.Length);
            Array.Copy(buffer, offset, newInitialPayload, InitialPayload.Length, count);
            InitialPayload = newInitialPayload;
            Logger.Log(id, InitialPayload.Length.ToString());
        }

        /// <summary>
        /// Returns true if the cryptor is ready to encrypt and decrypt
        /// </summary>
        public bool IsReady()
        {
            return isReady;
        }
        #endregion

        #region Diffie-Hellman Key Exchange Functions

        /// <summary>
        /// Send Y to the remote client, with a random padding that is 0 to 512 bytes long
        /// (Either "1 A->B: Diffie Hellman Ya, PadA" or "2 B->A: Diffie Hellman Yb, PadB")
        /// </summary>
        protected void SendY()
        {
            byte[] padLengthBytes = new byte[4];
            random.GetBytes(padLengthBytes);

            byte[] toSend = new byte[96 + (BitConverter.ToInt32(padLengthBytes, 0) & 0x1ff)];
            random.GetBytes(toSend);

            Array.Copy(Y, toSend, 96);

            SendMessage(toSend);
        }

        /// <summary>
        /// Receive the first 768 bits of the transmission from the remote client, which is Y in the protocol
        /// (Either "1 A->B: Diffie Hellman Ya, PadA" or "2 B->A: Diffie Hellman Yb, PadB")
        /// </summary>
        protected void ReceiveY()
        {
            OtherY = new byte[96];
            ReceiveMessage(OtherY, 96, doneReceiveYCallback);
        }

        protected virtual void doneReceiveY(IAsyncResult result)
        {
            S = ModuloCalculator.Calculate(OtherY, X);
        }

        #endregion

        #region Synchronization functions
        /// <summary>
        /// Read data from the socket until the byte string in syncData is read, or until syncStopPoint
        /// is reached (in that case, there is an EncryptionError).
        /// (Either "3 A->B: HASH('req1', S)" or "4 B->A: ENCRYPT(VC)")
        /// </summary>
        /// <param name="syncData">Buffer with the data to synchronize to</param>
        /// <param name="syncStopPoint">Maximum number of bytes (measured from the total received from the socket since connection) to read before giving up</param>
        protected void Synchronize(byte[] syncData, int syncStopPoint)
        {
            // The strategy here is to create a window the size of the data to synchronize and just refill that until its contents match syncData
            synchronizeData = syncData;
            synchronizeWindow = new byte[syncData.Length];
            this.syncStopPoint = syncStopPoint;

            if (bytesReceived > syncStopPoint)
            {
                EncryptionError();
                return;
            }

            if (socket == null)
            {
                IOError();
                return;
            }

            try
            {
                socket.BeginReceive(synchronizeWindow, 0, synchronizeWindow.Length, SocketFlags.None, fillSynchronizeBytesCallback, 0);
            }
            catch (Exception)
            {
                IOError();
                return;
            }
        }

        protected void fillSynchronizeBytes(IAsyncResult result)
        {
            lastActivity = DateTime.Now;

            if (socket == null)
            {
                IOError();
                return;
            }

            int read;

            try
            {
                read = socket.EndReceive(result);
                bytesReceived += read;
            }
            catch (Exception)
            {
                IOError();
                return;
            }

            if (read == 0 || !socket.Connected)
            {
                IOError();
                return;
            }

            int matchStart = -1; // offset of the beginning of the current match
            int filled = (int)result.AsyncState + read; // count of the bytes currently in synchronizeWindow
            int syncDataPtr = 0; // offset of the byte in synchronizeData we are currently matching

            for (int i = 0; i < filled; i++)
            {
                if (synchronizeData[syncDataPtr] != synchronizeWindow[i])
                {
                    matchStart = -1;
                    syncDataPtr = 0;
                }
                else
                {
                    if (matchStart == -1)
                        matchStart = i;

                    syncDataPtr++;
                }
            }

            if (matchStart == 0) // the match started in the beginning of the window, so it must be a full match
            {
                doneSynchronizeCallback(result);
            }
            else
            {
                if (bytesReceived > syncStopPoint)
                {
                    EncryptionError();
                    return;
                }

                if (matchStart != -1) // there's a partial match beginning in the middle of the window
                {
                    // move the partial match to the beginning of the window
                    for (int i = matchStart; i < synchronizeWindow.Length; i++)
                    {
                        synchronizeWindow[i - matchStart] = synchronizeWindow[i];
                    }

                    // fill the rest of the window
                    socket.BeginReceive(synchronizeWindow, synchronizeWindow.Length - matchStart, matchStart, SocketFlags.None, fillSynchronizeBytesCallback, (synchronizeWindow.Length - matchStart));
                }
                else // there's no match in this window
                {
                    try
                    {
                        socket.BeginReceive(synchronizeWindow, 0, synchronizeWindow.Length, SocketFlags.None, fillSynchronizeBytesCallback, 0);
                    }
                    catch (Exception)
                    {
                        IOError();
                        return;
                    }
                }
            }
        }

        protected virtual void doneSynchronize(IAsyncResult result)
        {
            // do nothing for now
        }
        #endregion

        #region I/O Functions
        protected void ReceiveMessage(byte[] buffer, int length, AsyncCallback callback)
        {
            if (length > 0)
            {
                if (initialBuffer != null)
                {
                    int toCopy = Math.Min(initialBufferCount, length);
                    Array.Copy(initialBuffer, 0, buffer, initialBufferOffset, toCopy);

                    if (toCopy == initialBufferCount)
                        initialBuffer = null;
                    else
                    {
                        initialBufferOffset += toCopy;
                        initialBufferCount -= toCopy;
                    }

                    if (toCopy == length)
                    {
                        callback(null);
                    }
                    else
                    {
                        try
                        {
                            socket.BeginReceive(buffer, toCopy, length - toCopy, SocketFlags.None, doneReceiveCallback, new object[] { callback, buffer, toCopy, length - toCopy });
                        }
                        catch (Exception)
                        {
                            IOError();
                            return;
                        }
                    }
                }
                else
                {
                    try
                    {
                        socket.BeginReceive(buffer, 0, length, SocketFlags.None, doneReceiveCallback, new object[] { callback, buffer, 0, length });
                    }
                    catch (Exception)
                    {
                        IOError();
                        return;
                    }
                }
            }
            else
            {
                callback(null);
            }
        }

        private void doneReceive(IAsyncResult result)
        {
            lastActivity = DateTime.Now;

            object[] receiveData = (object[])result.AsyncState;

            AsyncCallback callback = (AsyncCallback)receiveData[0];
            byte[] buffer = (byte[])receiveData[1];
            int start = (int)receiveData[2];
            int length = (int)receiveData[3];

            int received;

            try
            {
                received = socket.EndReceive(result);
                bytesReceived += received;
            }
            catch (Exception)
            {
                IOError();
                return;
            }

            if (received == 0 || !socket.Connected)
            {
                IOError();
                return;
            }

            if (received < length)
            {
                receiveData[2] = start + received;
                receiveData[3] = length - received;
                socket.BeginReceive(buffer, start + received, length - received, SocketFlags.None, doneReceiveCallback, receiveData);
            }
            else
            {
                callback(result);
            }
        }

        protected void SendMessage(byte[] toSend)
        {
            if (toSend.Length > 0)
            {
                try
                {
                    socket.BeginSend(toSend, 0, toSend.Length, SocketFlags.None, doneSendCallback, new object[] { toSend, 0, toSend.Length });
                }
                catch (Exception)
                {
                    IOError();
                    return;
                }
            }
        }

        private void doneSend(IAsyncResult result)
        {
            object[] sendData = (object[])result.AsyncState;

            byte[] toSend = (byte[])sendData[0];
            int start = (int)sendData[1];
            int length = (int)sendData[2];

            int sent;

            try
            {
                sent = socket.EndSend(result);
            }
            catch (Exception)
            {
                IOError();
                return;
            }

            if (sent == 0 || !socket.Connected)
            {
                IOError();
                return;
            }

            if (sent < length)
            {
                sendData[1] = start + sent;
                sendData[2] = length - sent;
                socket.BeginSend(toSend, start + sent, length - sent, SocketFlags.None, doneSendCallback, sendData);
            }
        }

        protected void IOError()
        {
            onEncryptorIOError(id);
        }

        protected void EncryptionError()
        {
            onEncryptorEncryptionError(id);
        }
        #endregion

        #region Cryptography Setup
        /// <summary>
        /// Generate a 160 bit random number for X
        /// </summary>
        private void GenerateX()
        {
            X = new byte[96];

            random.GetNonZeroBytes(X);

            for (int i = 0; i < 76; i++)
                X[i] = 0;
        }

        /// <summary>
        /// Calculate 2^X mod P
        /// </summary>
        private void GenerateY()
        {
            Y = ModuloCalculator.Calculate(ModuloCalculator.TWO, X);
        }

        /// <summary>
        /// Instantiate the cryptors with the keys: Hash(encryptionSalt, S, SKEY) for the encryptor and
        /// Hash(encryptionSalt, S, SKEY) for the decryptor.
        /// (encryptionSalt should be "keyA" if you're A, "keyB" if you're B, and reverse for decryptionSalt)
        /// </summary>
        /// <param name="encryptionSalt">The salt to calculate the encryption key with</param>
        /// <param name="decryptionSalt">The salt to calculate the decryption key with</param>
        protected void CreateCryptors(string encryptionSalt, string decryptionSalt)
        {
            encryptor = new RC4(Hash(Encoding.ASCII.GetBytes(encryptionSalt), S, SKEY));
            decryptor = new RC4(Hash(Encoding.ASCII.GetBytes(decryptionSalt), S, SKEY));
        }

        /// <summary>
        /// Sets CryptoSelect and initializes the stream encryptor and decryptor based on the selected method.
        /// </summary>
        /// <param name="remoteCryptoBytes">The cryptographic methods supported/wanted by the remote client in CryptoProvide format. The highest order one available will be selected</param>
        protected virtual int SelectCrypto(byte[] remoteCryptoBytes)
        {
            int selected = 0;

            CryptoSelect = new byte[remoteCryptoBytes.Length];

            for (int i = 0; i < CryptoSelect.Length; i++)
            {
                CryptoSelect[i] = 0;
            }

            for (int i = 0; i < remoteCryptoBytes.Length; i++)
            {
                byte intersection = (byte)(remoteCryptoBytes[i] & CryptoProvide[i]);

                if (intersection == 0)
                    continue;

                // Bump off all the rightmost bits, from left to right until we find a non zero one.
                for (int j = 7; j >= 0; j--)
                {
                    if ((intersection >> j) != 0)
                    {
                        CryptoSelect[i] = (byte)((byte)(intersection >> j) << j);
                        selected = j + ((remoteCryptoBytes.Length - i - 1) * 8) + 1;

                        break;
                    }
                }

                if (selected > 0)
                    break;
            }

            if (selected >= (int)minCryptoAllowed)
            {
                switch ((EncryptionType)selected)
                {
                    case EncryptionType.RC4Header:
                        streamEncryptor = NullEncryption.NullEncryptor;
                        streamDecryptor = NullEncryption.NullEncryptor;
                        break;
                    case EncryptionType.RC4Full:
                        streamEncryptor = encryptor;
                        streamDecryptor = decryptor;
                        break;
                }
            }

            return selected;
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
            byte[] combined = new byte[totalLength];

            foreach (byte[] datum in data)
            {
                totalLength += datum.Length;
            }

            combined = new byte[totalLength];

            for (int i = 0; i < data.Length; i++)
            {
                data[i].CopyTo(combined, cursor);
                cursor += data[i].Length;
            }

            return combined;
        }

        /// <summary>
        /// Hash some data with SHA1
        /// </summary>
        /// <param name="data">Buffers to hash</param>
        /// <returns>20-byte hash</returns>
        protected byte[] Hash(params byte[][] data)
        {
            return hasher.ComputeHash(Combine(data));
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
            byte[] padLengthBytes = new byte[4];
            random.GetBytes(padLengthBytes);

            byte[] pad = new byte[BitConverter.ToInt32(padLengthBytes, 0) & 0x1ff];

            for (int i = 0; i < pad.Length; i++)
            {
                pad[i] = 0;
            }

            return pad;
        }
        #endregion

        #region Miscellaneous
        /// <summary>
        /// Encrypts some data with the RC4 encryptor used in handshaking
        /// </summary>
        /// <param name="buffer">Buffer with the data to encrypt</param>
        /// <param name="offset">Offset to begin encryption</param>
        /// <param name="count">Number of bytes to encrypt</param>
        protected void DoEncrypt(byte[] data, int offset, int length)
        {
            encryptor.InPlaceCrypt(data, offset, length);
        }

        /// <summary>
        /// Decrypts some data with the RC4 decryptor used in handshaking
        /// </summary>
        /// <param name="buffer">Buffer with the data to decrypt</param>
        /// <param name="offset">Offset to begin decryption</param>
        /// <param name="count">Number of bytes to decrypt</param>
        protected void DoDecrypt(byte[] data, int offset, int length)
        {
            decryptor.InPlaceCrypt(data, offset, length);
        }

        /// <summary>
        /// Encrypts some data with the RC4 encryptor used in handshaking
        /// </summary>
        /// <param name="data...">Buffers with the data to encrypt</param>
        /// <returns>Buffer with encrypted data</returns>
        protected byte[] DoEncrypt(params byte[][] data)
        {
            return encryptor.DoCrypt(Combine(data));
        }

        /// <summary>
        /// Decrypts some data with the RC4 encryptor used in handshaking
        /// </summary>
        /// <param name="data">Buffers with the data to decrypt</param>
        /// <returns>Buffer with decrypted data</returns>
        protected byte[] DoDecrypt(byte[] data)
        {
            return decryptor.DoCrypt(data);
        }

        /// <summary>
        /// Signal that the cryptor is now in a state ready to encrypt and decrypt payload data
        /// </summary>
        protected void Ready()
        {
            // Send any remaining initial payload data that we hadn't gotten a chance to send
            Encrypt(InitialPayload, 0, InitialPayload.Length);
            SendMessage(InitialPayload);

            isReady = true;

            onEncryptorReady(id);
        }

        protected void SetMinCryptoAllowed(EncryptionType minCryptoAllowed)
        {
            this.minCryptoAllowed = minCryptoAllowed;

            // EncryptionType is basically a bit position starting from the right.
            // This sets all bits in CryptoProvide 0 that is to the right of minCryptoAllowed.
            if ((int)minCryptoAllowed > 0)
            {
                int mByte = CryptoProvide.Length - 1 - ((int)minCryptoAllowed - 1) / 8;
                int mBit = ((int)minCryptoAllowed - 1) % 8;

                for (int i = (CryptoProvide.Length - 1); i > mByte; i--)
                    CryptoProvide[i] = 0;

                CryptoProvide[mByte] &= (byte)(0xff << mBit);
            }
        }
        #endregion
    }
}
