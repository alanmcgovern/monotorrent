//
// ToolboxTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class ToolboxTests
    {
        [Test]
        public void ByteMatch_DifferentArrayLengths ()
        {
            Assert.IsFalse (Toolbox.ByteMatch (new byte[1], new byte[2]));
        }

        [Test]
        public void ByteMatch_DifferentArrayLengths2()
        {
            Assert.IsFalse (Toolbox.ByteMatch (new byte[1], 0, new byte[2], 0, 2));
            Assert.IsTrue (Toolbox.ByteMatch (new byte[1], 0, new byte[2], 0, 1));
        }

        [Test]
        public void ByteMatch_Null ()
        {
            Assert.Throws<ArgumentNullException> (() => Toolbox.ByteMatch (null, new byte[1]));
            Assert.Throws<ArgumentNullException> (() => Toolbox.ByteMatch (new byte[1], null));

            Assert.Throws<ArgumentNullException> (() => Toolbox.ByteMatch (null, 0, new byte[2], 0, 2));
            Assert.Throws<ArgumentNullException> (() => Toolbox.ByteMatch (new byte[1], 0, null, 0, 2));
        }
    }
}
