//
// Cache.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Collections.Generic;
using System.Threading;

namespace MonoTorrent
{
    class SpinLocked
    {
        internal static SpinLocked<T> Create<T> (T value)
        {
            return SpinLocked<T>.Create (value);
        }
    }

    class SpinLocked<T>
    {
        SpinLock Lock = new SpinLock (false);
        T Value { get; }

        internal static SpinLocked<T> Create (T value)
        {
            return new SpinLocked<T> (value);
        }

        SpinLocked (T value)
        {
            Value = value;
        }

        public Releaser Enter (out T value)
        {
            // Nothing in this library is safe after a thread abort... so let's not care too much about this.
            bool taken = false;
            Lock.Enter (ref taken);

            value = Value;
            return new Releaser (this);
        }

        public struct Releaser : IDisposable
        {
            SpinLocked<T> SpinLocked;

            internal Releaser (SpinLocked<T> ssl)
                => SpinLocked = ssl;

            public void Dispose ()
                => SpinLocked?.Lock.Exit (false);
        }
    }
}
