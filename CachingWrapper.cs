using System;
using System.Collections.Generic;
using System.Threading;

namespace Utilities
{
    /// <summary>
    /// A thread safe wrapper around a db or service call which provides LRU Caching.
    /// by dpan, 03/2009
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    public class CachingWrapper<TKey, TValue> where TKey : IComparable
    {
        /// <summary>
        /// The delegate method that will be called when a request cannot be fullfilled from the cache
        /// </summary>
        /// <param name="key">The Key</param>
        /// <returns>The value</returns>
        public delegate TValue RetrieveFromOriginalSource(TKey key);

        #region Data Members

        private readonly Dictionary<TKey, TValue> _localCache;
        private readonly IndexedLinkedList<TKey> _lruList;
        private readonly int _capacity;

        private readonly RetrieveFromOriginalSource _originalSourceRetriever;

        // The lockManager is used to provide Thread Safety when accessing our main dictionary object (_localCache)
        private readonly ReaderWriterLockSlim _lockManager = new ReaderWriterLockSlim();

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingWrapper{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="originalSourceRetriever">The method that will return an element if it is not in the cache.</param>
        public CachingWrapper(RetrieveFromOriginalSource originalSourceRetriever : this(originalSourceRetriever, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingWrapper{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="originalSourceRetriever">The method that will return an element if it is not in the cache.</param>
        /// <param name="capacity">The maximum capacity of the cache. Use zero for infinite, non LRU capacity</param>
        public CachingWrapper(RetrieveFromOriginalSource originalSourceRetriever, int capacity)
        {
            if (originalSourceRetriever == null)
            {
                throw new ArgumentException("originalSourcerRetriever cannot be null");
            }

            _originalSourceRetriever += originalSourceRetriever;
            _capacity = capacity;

            if (capacity > 0)
            {
                _localCache = new Dictionary<TKey, TValue>(capacity);
                _lruList = new IndexedLinkedList<TKey>();
            }
            else
            {
                _localCache = new Dictionary<TKey, TValue>();
            }
        }

        /// <summary>
        /// Retrieves the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The actual value</returns>
        public TValue Retrieve(TKey key)
        {
            return Retrieve(key, Timeout.Infinite);
        }

        /// <summary>
        /// Retrieves the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="forceFresh">if set to <c>true</c> forces a refresh of the cache from the source.</param>
        /// <returns>The actual value</returns>
        public TValue Retrieve(TKey key, bool forceFresh)
        {
            return forceFresh ? FetchFromOriginalSource(key, Timeout.Infinite) : Retrieve(key);
        }

        /// <summary>
        /// Retrieves the specified key within a specified timeout
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="lockTimeout">The lock timeout in milliseconds. The total time that the method is actually going to wait can be up to twice that amount.</param>
        /// <returns>The actual value</returns>
        public TValue Retrieve(TKey key, int lockTimeout)
        {
            if (_lockManager.TryEnterUpgradeableReadLock(lockTimeout))
            {
                try
                {
                    TValue result;

                    if (!_localCache.TryGetValue(key, out result))
                    {
                        result = _originalSourceRetriever(key);
                        WriteToCache(key, result, lockTimeout);
                    }

                    PutKeyOnTop(key);

                    return result;
                }
                finally
                {
                    _lockManager.ExitUpgradeableReadLock();
                }
            }

            return FetchFromOriginalSource(key, lockTimeout);
        }

        /// <summary>
        /// Clears the entire cache.
        /// </summary>
        public void ClearCache()
        {
            ClearCache(Timeout.Infinite);
        }

        /// <summary>
        /// Clears the entire cache within the specified timeout.
        /// </summary>
        /// <param name="lockTimeout">The lock timeout in milliseconds.</param>
        public bool ClearCache(int lockTimeout)
        {
            if (_lockManager.TryEnterWriteLock(lockTimeout))
            {
                try
                {
                    _localCache.Clear();
                    _lruList.Clear();

                    return true;
                }
                finally
                {
                    _lockManager.ExitWriteLock();
                }
            }

            return false;
        }

        private TValue FetchFromOriginalSource(TKey key, int lockTimeout)
        {
            TValue result = _originalSourceRetriever(key);

            if (WriteToCache(key, result, lockTimeout))
            {
                PutKeyOnTop(key);
            }

            return result;
        }

        private void PutKeyOnTop(TKey key)
        {
            if (_capacity > 0)
            {
                // Remove it from current position
                _lruList.Remove(key);

                // Add it again, this will result in it being placed on top
                _lruList.Add(key);
            }
        }

        private bool WriteToCache(TKey key, TValue value, int lockTimeout)
        {
            if (_lockManager.TryEnterWriteLock(lockTimeout))
            {
                try
                {
                    if (_localCache.ContainsKey(key))
                    {
                        _localCache[key] = value; // refresh

                        PutKeyOnTop(key);
                    }
                    else
                    {
                        _localCache.Add(key, value);

                        if (_capacity > 0)
                        {
                            if (_localCache.Count > _capacity)
                            {
                                _localCache.Remove(_lruList.First);
                                _lruList.RemoveFirst();
                            }

                            _lruList.Add(key);
                        }
                    }

                    return true;
                }
                finally
                {
                    _lockManager.ExitWriteLock();
                }
            }

            return false;
        }
    }
}