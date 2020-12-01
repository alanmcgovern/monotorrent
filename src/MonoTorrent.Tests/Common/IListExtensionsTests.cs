﻿//
// IListExtensionsTests.cs
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


using System.Collections.Generic;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class IListExtensionsTests
    {
        [Test]
        public void EmptyList ()
        {
            Assert.AreEqual (-1, new List<int> ().BinarySearch (val => 5));
            Assert.AreEqual (-1, new List<int> ().BinarySearch ((val, state) => state, 5));
        }

        [Test]
        public void FindAtStart ()
        {
            Assert.AreEqual (0, new List<int> { 5, 7, 9 }.BinarySearch (val => val.CompareTo (5)));
            Assert.AreEqual (0, new List<int> { 5, 7, 9 }.BinarySearch ((val, state) => val.CompareTo (state), 5));
        }

        [Test]
        public void FindAtMiddle ()
        {
            Assert.AreEqual (1, new List<int> { 1, 5, 10 }.BinarySearch (val => val.CompareTo (5)));
            Assert.AreEqual (1, new List<int> { 1, 5, 10 }.BinarySearch ((val, state) => val.CompareTo (state), 5));
        }

        [Test]
        public void FindAtEnd ()
        {
            Assert.AreEqual (2, new List<int> { 1, 3, 5 }.BinarySearch (val => val.CompareTo (5)));
            Assert.AreEqual (2, new List<int> { 1, 3, 5 }.BinarySearch ((val, state) => val.CompareTo (state), 5));
        }

        [Test]
        public void InsertAtStart ()
        {
            Assert.AreEqual (~0, new List<int> { 10 }.BinarySearch (val => val.CompareTo (5)));
            Assert.AreEqual (~0, new List<int> { 10 }.BinarySearch ((val, state) => val.CompareTo (state), 5));
        }

        [Test]
        public void InsertAtMiddle ()
        {
            Assert.AreEqual (~2, new List<int> { 1, 2, 9, 10 }.BinarySearch (val => val.CompareTo (5)));
            Assert.AreEqual (~2, new List<int> { 1, 2, 9, 10 }.BinarySearch ((val, state) => val.CompareTo (state), 5));
        }

        [Test]
        public void InsertAtEnd ()
        {
            Assert.AreEqual (~1, new List<int> { 1 }.BinarySearch (val => val.CompareTo (5)));
            Assert.AreEqual (~1, new List<int> { 1 }.BinarySearch ((val, state) => val.CompareTo (state), 5));
        }
    }
}
