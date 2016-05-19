//
// System.Web.HttpUtility/HttpEncoder
//
// Authors:
//   Patrik Torstensson (Patrik.Torstensson@labs2.com)
//   Wictor Wilén (decode/encode functions) (wictor@ibizkit.se)
//   Tim Coleman (tim@timcoleman.com)
//   Gonzalo Paniagua Javier (gonzalo@ximian.com)
//   Marek Habersack <mhabersack@novell.com>
//
// Copyright (C) 2005-2010 Novell, Inc (http://www.novell.com)
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

// THIS FILE IS COPIED/PASTED FROM THE MONO SOURCE TREE TO AVOID
// A DEPENDENCY ON SYSTEM.WEB WHICH IS NOT ALWAYS AVAILABLE.

using System;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace MonoTorrent
{
    internal static class UriHelper
    {
        private static readonly char[] hexChars = "0123456789abcdef".ToCharArray();

        public static string UrlEncode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");

            var result = new MemoryStream(bytes.Length);
            for (var i = 0; i < bytes.Length; i++)
                UrlEncodeChar((char) bytes[i], result, false);

            return Encoding.ASCII.GetString(result.ToArray());
        }

        public static byte[] UrlDecode(string s)
        {
            if (null == s)
                return null;

            var e = Encoding.UTF8;
            if (s.IndexOf('%') == -1 && s.IndexOf('+') == -1)
                return e.GetBytes(s);

            long len = s.Length;
            var bytes = new List<byte>();
            int xchar;
            char ch;

            for (var i = 0; i < len; i++)
            {
                ch = s[i];
                if (ch == '%' && i + 2 < len && s[i + 1] != '%')
                {
                    if (s[i + 1] == 'u' && i + 5 < len)
                    {
                        // unicode hex sequence
                        xchar = GetChar(s, i + 2, 4);
                        if (xchar != -1)
                        {
                            WriteCharBytes(bytes, (char) xchar, e);
                            i += 5;
                        }
                        else
                            WriteCharBytes(bytes, '%', e);
                    }
                    else if ((xchar = GetChar(s, i + 1, 2)) != -1)
                    {
                        WriteCharBytes(bytes, (char) xchar, e);
                        i += 2;
                    }
                    else
                    {
                        WriteCharBytes(bytes, '%', e);
                    }
                    continue;
                }

                if (ch == '+')
                    WriteCharBytes(bytes, ' ', e);
                else
                    WriteCharBytes(bytes, ch, e);
            }

            return bytes.ToArray();
        }

        private static void UrlEncodeChar(char c, Stream result, bool isUnicode)
        {
            if (c > ' ' && NotEncoded(c))
            {
                result.WriteByte((byte) c);
                return;
            }
            if (c == ' ')
            {
                result.WriteByte((byte) '+');
                return;
            }
            if ((c < '0') ||
                (c < 'A' && c > '9') ||
                (c > 'Z' && c < 'a') ||
                (c > 'z'))
            {
                if (isUnicode && c > 127)
                {
                    result.WriteByte((byte) '%');
                    result.WriteByte((byte) 'u');
                    result.WriteByte((byte) '0');
                    result.WriteByte((byte) '0');
                }
                else
                    result.WriteByte((byte) '%');

                var idx = (int) c >> 4;
                result.WriteByte((byte) hexChars[idx]);
                idx = (int) c & 0x0F;
                result.WriteByte((byte) hexChars[idx]);
            }
            else
            {
                result.WriteByte((byte) c);
            }
        }

        private static int GetChar(string str, int offset, int length)
        {
            var val = 0;
            var end = length + offset;
            for (var i = offset; i < end; i++)
            {
                var c = str[i];
                if (c > 127)
                    return -1;

                var current = GetInt((byte) c);
                if (current == -1)
                    return -1;
                val = (val << 4) + current;
            }

            return val;
        }

        private static int GetInt(byte b)
        {
            var c = (char) b;
            if (c >= '0' && c <= '9')
                return c - '0';

            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;

            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;

            return -1;
        }

        private static bool NotEncoded(char c)
        {
            return c == '!' || c == '(' || c == ')' || c == '*' || c == '-' || c == '.' || c == '_' || c == '\'';
        }

        private static void WriteCharBytes(List<byte> buf, char ch, Encoding e)
        {
            if (ch > 255)
            {
                foreach (var b in e.GetBytes(new char[] {ch}))
                    buf.Add(b);
            }
            else
                buf.Add((byte) ch);
        }
    }
}