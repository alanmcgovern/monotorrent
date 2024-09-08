//
// LoggerFactory.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2024 Alan McGovern
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

namespace MonoTorrent.Logging
{
    public static class LoggerFactory
    {
        /// <summary>
        /// The factory method used to create new ILogger instances. The <see cref="string"/> parameter
        /// is the <see cref="Type.FullName"/> for the class the ILogger is associated with. You can
        /// return <see langword="null"/> for any class to disable logging for that class.
        /// </summary>
        public static IRootLogger RootLogger { get; private set; } = new NullLogger ();

        public static ILogger Create (string name)
            => new Logger (name);

        public static void Register (IRootLogger rootLogger)
            => RootLogger = rootLogger ?? new NullLogger ();
    }
}
