using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    using System;
    using System.Buffers;
    using System.IO;

    internal static class TorrentFilePathEscaper
    {
        private static readonly SearchValues<char> InvalidPathChars =
            SearchValues.Create (Path.GetInvalidPathChars ());

        private static readonly SearchValues<char> InvalidFileNameChars =
            SearchValues.Create (Path.GetInvalidFileNameChars ());

        internal static string PathEscape (string path)
        {
            ReadOnlySpan<char> src = path;

            // Fast path: no invalid chars
            if (src.IndexOfAny (InvalidPathChars) < 0)
                return path;

            int newLength = CalculateEscapedPathLength (src);

            return string.Create (newLength, path, static (dest, p) =>
            {
                ReadOnlySpan<char> source = p;
                int pos = 0;

                foreach (char c in source) {
                    if (InvalidPathChars.Contains (c)) {
                        if (((int) c).TryFormat (dest[pos..], out int written, "X"))
                            pos += written;
                        else
                            throw new InvalidOperationException ("Could not replace an invalid character in the string");
                    } else {
                        dest[pos++] = c;
                    }
                }
            });
        }

        internal static string PathAndFileNameEscape (string path)
        {
            ReadOnlySpan<char> src = path;

            int sep = src.LastIndexOf (Path.DirectorySeparatorChar);

            ReadOnlySpan<char> dir =
                sep >= 0 ? src[..sep] : ReadOnlySpan<char>.Empty;

            ReadOnlySpan<char> file =
                sep >= 0 ? src[(sep + 1)..] : src;

            bool dirNeedsEscape =
                dir.IndexOfAny (InvalidPathChars) >= 0;

            bool fileNeedsEscape =
                file.IndexOfAny (InvalidFileNameChars) >= 0;

            // Fast path: nothing changes
            if (!dirNeedsEscape && !fileNeedsEscape)
                return path;

            int newLength =
                CalculateEscapedPathLength (dir) +
                (sep >= 0 ? 1 : 0) +
                CalculateEscapedFileLength (file);

            return string.Create (
                newLength,
                (path, hasDir: sep >= 0),
                static (dest, state) => {
                    int sep = state.path.LastIndexOf (Path.DirectorySeparatorChar);
                    ReadOnlySpan<char> dir = sep >= 0 ? state.path[..sep] : ReadOnlySpan<char>.Empty;
                    ReadOnlySpan<char> file = sep >= 0 ? state.path[(sep + 1)..] : state.path;

                    int pos = 0;

                    foreach (char c in dir) {
                        if (InvalidPathChars.Contains (c)) {
                            if (((int) c).TryFormat (
                                dest[pos..],
                                out int written,
                                "X"))
                                pos += written;
                            else
                                throw new NotSupportedException ("Could not replace an invalid character in the path");
                        } else {
                            dest[pos++] = c;
                        }
                    }

                    if (state.hasDir)
                        dest[pos++] = Path.DirectorySeparatorChar;

                    foreach (char c in file) {
                        if (InvalidFileNameChars.Contains (c)) {
                            dest[pos++] = '_';

                            if (((int) c).TryFormat (
                                dest[pos..],
                                out int written,
                                "X"))
                                pos += written;
                            else
                                throw new NotSupportedException ("Could not replace an invalid character in the path");

                            dest[pos++] = '_';
                        } else {
                            dest[pos++] = c;
                        }
                    }
                });
        }

        private static int CalculateEscapedPathLength (
            ReadOnlySpan<char> span)
        {
            int length = span.Length;

            foreach (char c in span) {
                if (InvalidPathChars.Contains (c)) {
                    length += HexLength (c) - 1;
                }
            }

            return length;
        }

        private static int CalculateEscapedFileLength (
            ReadOnlySpan<char> span)
        {
            int length = span.Length;

            foreach (char c in span) {
                if (InvalidFileNameChars.Contains (c)) {
                    // replace 1 char with _XX_
                    length += HexLength (c) + 1;
                }
            }

            return length;
        }

        private static int HexLength (char c)
        {
            int value = c;

            if (value <= 0xF)
                return 1;

            if (value <= 0xFF)
                return 2;

            if (value <= 0xFFF)
                return 3;

            return 4;
        }
    }
}
