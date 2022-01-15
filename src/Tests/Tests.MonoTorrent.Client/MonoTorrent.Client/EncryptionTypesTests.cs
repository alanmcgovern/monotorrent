//
// EncryptionTypeTests.cs
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
using System.Collections.Generic;
using System.Linq;

using MonoTorrent.Connections;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class EncryptionTypesTests
    {
        [Test]
        public void GetSupported ()
        {
            var result = EncryptionTypes.GetSupportedEncryption (
                new[] { EncryptionType.RC4Full, EncryptionType.PlainText },
                new[] { EncryptionType.PlainText });
            Assert.AreEqual (EncryptionType.PlainText, result.Single ());

            result = EncryptionTypes.GetSupportedEncryption (
                new[] { EncryptionType.PlainText },
                new[] { EncryptionType.RC4Full, EncryptionType.PlainText });
            Assert.AreEqual (EncryptionType.PlainText, result.Single ());

            result = EncryptionTypes.GetSupportedEncryption (
                new[] { EncryptionType.PlainText, EncryptionType.RC4Full, EncryptionType.RC4Header },
                new[] { EncryptionType.RC4Full, EncryptionType.PlainText });
            Assert.AreEqual (EncryptionType.RC4Full, result.First ());
            Assert.AreEqual (EncryptionType.PlainText, result.Last ());
        }

        [Test]
        public void GetPreferredEncryption ()
        {
            var result = EncryptionTypes.GetPreferredEncryption (
                new[] { EncryptionType.PlainText },
                new[] { EncryptionType.RC4Header });
            Assert.IsEmpty (result);

            result = EncryptionTypes.GetPreferredEncryption (
                new[] { EncryptionType.PlainText, EncryptionType.RC4Header },
                new[] { EncryptionType.RC4Header });

            Assert.AreEqual (EncryptionType.RC4Header, result.Single ());

            result = EncryptionTypes.GetPreferredEncryption (
                new[] { EncryptionType.PlainText, EncryptionType.RC4Header, EncryptionType.RC4Full },
                new[] { EncryptionType.RC4Full });

            Assert.AreEqual (EncryptionType.RC4Full, result.Single ());

            result = EncryptionTypes.GetPreferredEncryption (
                new[] { EncryptionType.PlainText, EncryptionType.RC4Header },
                new[] { EncryptionType.RC4Full, EncryptionType.PlainText, EncryptionType.RC4Header });

            Assert.AreEqual (EncryptionType.PlainText, result.Single ());
        }

        [Test]
        public void RemoveFromEmptyList ()
        {
            var result = EncryptionTypes.Remove (new List<EncryptionType> (), EncryptionType.PlainText);
            Assert.IsEmpty (result);
            Assert.Throws<NotSupportedException> (() => result.Add (EncryptionType.PlainText));
        }
    }
}
