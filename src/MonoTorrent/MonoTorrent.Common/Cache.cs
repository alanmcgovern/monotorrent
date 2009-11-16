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
using System.Text;

namespace MonoTorrent.Common
{
    interface ICache<T>
    {
        int Count { get; }
        T Dequeue();
        void Enqueue (T instance);
    }

    class Cache<T> : ICache<T>
        where T : class, ICacheable, new()
    {
        bool autoCreate;
        Queue<T> cache;

        public int Count
        {
            get { return cache.Count; }
        }

        public Cache()
            : this (false)
        {

        }

        public Cache(bool autoCreate)
        {
            this.autoCreate = autoCreate;
            this.cache = new Queue<T>();
        }

        public T Dequeue()
        {
            if (cache.Count > 0)
                return cache.Dequeue();
            return autoCreate ? new T() : null;
        }

        public void Enqueue(T instance)
        {
            instance.Initialise();
            cache.Enqueue(instance);
        }
        public ICache<T> Synchronize()
        {
            return new SynchronizedCache<T>(this);
        }
    }

    class SynchronizedCache<T> : ICache<T>
    {
        ICache<T> cache;

        public int Count
        {
            get { return cache.Count; }
        }

        public SynchronizedCache(ICache<T> cache)
        {
            Check.Cache(cache);
            this.cache = cache;
        }

        public T Dequeue()
        {
            lock (cache)
                return cache.Dequeue();
        }

        public void Enqueue(T instance)
        {
            lock (cache)
                cache.Enqueue(instance);
        }
    }
}
