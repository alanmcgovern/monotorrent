using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoTorrent
{
    static class SpanExtensions
    {
#if NETSTANDARD2_0 || NETSTANDARD2_1
        public static bool Contains (this ReadOnlySpan<int> span, int value)
        {
            for (int i = 0; i < span.Length; i++)
                if (span[i] == value)
                    return true;
            return false;
        }
#endif
    }
}
