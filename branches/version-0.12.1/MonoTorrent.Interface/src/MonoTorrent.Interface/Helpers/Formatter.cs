/*
 * $Id: Formatter.cs 880 2006-08-19 22:50:54Z piotr $
 * Copyright (c) 2006 by Piotr Wolny <gildur@gmail.com>
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System.Text;

namespace MonoTorrent.Interface.Helpers
{
    public static class Formatter
    {
        public static string FormatSize(double size)
        {
            if (size < 1 << 10) {
                return string.Format("{0:N} B", size);
            }
            if (size < 1 << 20) {
                return string.Format("{0:N} KB", size / (1 << 10));
            }
            if (size < 1 << 30) {
                return string.Format("{0:N} MB", size / (1 << 20));
            }
            return string.Format("{0:N} GB", size / (1 << 30));
        }

        public static string FormatSpeed(double speed)
        {
            return string.Format("{0}/s", FormatSize(speed));
        }

        public static string FormatPercent(double percent)
        {
            return string.Format("{0:N}%", percent);
        }

        public static string FormatBytes(byte[] bytes)
        {
            StringBuilder bytesString = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++) {
                bytesString.Append(string.Format("{0:X2} ", bytes[i]));
            }
            return bytesString.ToString();
        }
    }
}
