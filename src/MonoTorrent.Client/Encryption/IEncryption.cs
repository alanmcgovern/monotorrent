using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Encryption
{
    internal interface IEncryption
    {
        byte[] DoCrypt(byte[] buffer);
        void InPlaceCrypt(byte[] buffer, int offset, int length);
    }
}
