using System.Collections.Generic;

namespace MonoTorrent.Common
{
    internal interface ICache<T>
    {
        int Count { get; }
        T Dequeue();
        void Enqueue(T instance);
    }

    internal class Cache<T> : ICache<T>
        where T : class, ICacheable, new()
    {
        private readonly bool autoCreate;
        private readonly Queue<T> cache;

        public Cache()
            : this(false)
        {
        }

        public Cache(bool autoCreate)
        {
            this.autoCreate = autoCreate;
            cache = new Queue<T>();
        }

        public int Count
        {
            get { return cache.Count; }
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

    internal class SynchronizedCache<T> : ICache<T>
    {
        private readonly ICache<T> cache;

        public SynchronizedCache(ICache<T> cache)
        {
            Check.Cache(cache);
            this.cache = cache;
        }

        public int Count
        {
            get { return cache.Count; }
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