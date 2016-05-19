#if !DISABLE_DHT
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    internal static class TransactionId
    {
        private static readonly byte[] current = new byte[2];

        public static BEncodedString NextId()
        {
            lock (current)
            {
                var result = new BEncodedString((byte[]) current.Clone());
                if (current[0]++ == 255)
                    current[1]++;
                return result;
            }
        }
    }
}

#endif