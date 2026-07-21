//
// SettingsValidators.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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


using System;

namespace MonoTorrent.Client
{
    static class SettingsValidators
    {
        internal static string CheckHttpStreamingPrefix (string value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));

            if (!value.EndsWith ("/"))
                throw new ArgumentException ("The HTTP prefix must end in '/' ", nameof (value));

            if (!value.StartsWith ("http://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith ("https://", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException ("The HTTP prefix must begin with 'http://' or 'https://'", nameof (value));

            return value;
        }

        internal static int CheckPort (int value)
        {
            if (value < -1 || value > ushort.MaxValue)
                throw new ArgumentOutOfRangeException (nameof (value), "Value should be a valid port number between 0 and 65535 inclusive, or -1 to disable listening for connections.");
            return value;
        }

        internal static int CheckZeroOrPositive (int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException (nameof (value), "Value should be zero or greater");
            return value;
        }

        internal static TimeSpan CheckZeroOrPositive (TimeSpan value)
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException ("value", "must be greater than or equal to TimeSpan.Zero");
            return value;
        }
    }
}
