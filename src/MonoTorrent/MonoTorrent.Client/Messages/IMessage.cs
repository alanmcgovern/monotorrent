using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Messages
{
    interface IMessage
    {
        int ByteLength { get;}

        byte[] Encode();
        int Encode(byte[] buffer, int offset);
        int Encode(ArraySegment<byte> buffer, int offset);

        void Decode(byte[] buffer, int offset, int length);
        void Decode(ArraySegment<byte> buffer, int offset, int length);
    }
}
