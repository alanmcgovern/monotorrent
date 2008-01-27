using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    interface IMessage
    {
        int ByteLength { get;}

        int Encode(byte[] buffer, int offset);
        int Encode(ArraySegment<byte> buffer, int offset);

        void Decode(byte[] buffer, int offset, int length);
        void Decode(ArraySegment<byte> buffer, int offset, int length);
    }
}
