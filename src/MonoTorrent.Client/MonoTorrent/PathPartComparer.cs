using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MonoTorrent
{
    sealed class PathPartComparer : IComparer<ReadOnlyMemory<char>>, IComparer<string>
    {
        public static PathPartComparer Instance { get; } = new ();

        public PathPartComparer ()
        {
        }

        public int Compare (string? x, string? y)
        {
            if (x is null)
                return y is null ? 0 : -1;
            if (y is null)
                return 1;
            return Compare (x.AsMemory (), y.AsMemory ());
        }

        public int Compare (ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
        {
            ReadOnlySpan<char> a = x.Span;
            ReadOnlySpan<char> b = y.Span;

            while (true) {
                bool aEmpty = a.IsEmpty;
                bool bEmpty = b.IsEmpty;

                if (aEmpty || bEmpty) {
                    if (aEmpty == bEmpty)
                        return 0;

                    return aEmpty ? -1 : 1;
                }

                int aSep = IndexOfSeparator (a);
                int bSep = IndexOfSeparator (b);

                ReadOnlySpan<char> aPart =
                    aSep >= 0 ? a[..aSep] : a;

                ReadOnlySpan<char> bPart =
                    bSep >= 0 ? b[..bSep] : b;

                int cmp = aPart.CompareTo (bPart, StringComparison.Ordinal);

                if (cmp != 0)
                    return cmp;

                a = aSep >= 0
                    ? a[(aSep + 1)..]
                    : default;

                b = bSep >= 0
                    ? b[(bSep + 1)..]
                    : default;
            }
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        private static int IndexOfSeparator (
            ReadOnlySpan<char> s)
        {
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];

                if (c == '/' || c == '\\')
                    return i;
            }

            return -1;
        }
    }
}
