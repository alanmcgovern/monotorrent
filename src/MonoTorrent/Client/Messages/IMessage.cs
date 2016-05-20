namespace MonoTorrent.Client.Messages
{
    internal interface IMessage
    {
        int ByteLength { get; }

        byte[] Encode();
        int Encode(byte[] buffer, int offset);

        void Decode(byte[] buffer, int offset, int length);
    }
}