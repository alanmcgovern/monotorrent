//
// HashAlgoFactory.cs
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
using System.Text;
using System.Security.Cryptography;

namespace MonoTorrent.Common
{
    public static class HashAlgoFactory
    {
        static Dictionary<Type, Type> algos = new Dictionary<Type, Type>();

        static HashAlgoFactory()
        {
            Register<MD5, MD5CryptoServiceProvider>();
            Register<SHA1, SHA1CryptoServiceProvider>();
        }

        public static void Register<T, U>()
            where T : HashAlgorithm
            where U : HashAlgorithm
        {
            Register(typeof(T), typeof(U));
        }

        public static void Register(Type baseType, Type specificType)
        {
            Check.BaseType(baseType);
            Check.SpecificType(specificType);

            lock (algos)
                algos[baseType] = specificType;
        }

        public static T Create<T>()
            where T : HashAlgorithm
        {
            if (algos.ContainsKey(typeof(T)))
                return (T)Activator.CreateInstance(algos[typeof(T)]);
            return null;
        }
    }
}
