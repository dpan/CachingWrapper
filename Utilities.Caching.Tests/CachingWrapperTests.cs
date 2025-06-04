using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utilities;

namespace Utilities.Tests
{
    public class IterationResults
    {
        #region Data Members

        public decimal MissRatio;
        public decimal MinMissRatio;
        public decimal MaxMissRatio;
        public decimal Elapsed;
        public decimal MinElapsed;
        public decimal MaxElapsed;

        #endregion
    }

    [TestClass]
    public class CachingWrapperTests
    {
        #region Data Members

        private int _cacheMisses;
        private Func<int, string>? _customDatabaseCallMock; // For custom mock behavior
        private Action<int>? _onCacheMiss; // Action to call on a cache miss, can be used for synchronization or logging

        #endregion

        [TestInitialize]
        public void TestInitialize()
        {
            _cacheMisses = 0;
            _customDatabaseCallMock = null;
            _onCacheMiss = null;
        }

        [Description("CachingWrapper tester")]
        [TestMethod]
        public void TestCachingWrapperSimple()
        {
            _cacheMisses = 0;
            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 0);

            Assert.AreEqual(MockedResult(0), cachedSource.Retrieve(0)); // First try should cache it (cache miss)
            Assert.AreEqual(MockedResult(0), cachedSource.Retrieve(0)); // Second try should retrieve it from cache
            Assert.AreEqual(1, _cacheMisses, "There should only be one miss!");
        }

        [Description("CachingWrapper tester")]
        [TestMethod]
        public void TestCachingWrapperWithCapacity()
        {
            _cacheMisses = 0;

            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 5);

            Assert.AreEqual(MockedResult(0), cachedSource.Retrieve(0)); // First try should cache it (cache miss)
            Assert.AreEqual(MockedResult(1), cachedSource.Retrieve(1)); // First try should cache it (cache miss)
            Assert.AreEqual(MockedResult(2), cachedSource.Retrieve(2)); // First try should cache it (cache miss)
            Assert.AreEqual(MockedResult(3), cachedSource.Retrieve(3)); // First try should cache it (cache miss)
            Assert.AreEqual(MockedResult(4), cachedSource.Retrieve(4)); // First try should cache it (cache miss)

            // Make sure 0 element is cached properly (should not increase the cache misses)
            // This should also put element 0 on top, making element 1 the last (and a candidate for removal)
            Assert.AreEqual(MockedResult(0), cachedSource.Retrieve(0), "Wrong element was returned from cache!");

            Assert.AreEqual(5, _cacheMisses, "There should be 5 cache misses so far");

            // The following statement should cause a capacity overflow which should discard the last element
            // The last element should be element 1
            Assert.AreEqual(MockedResult(5), cachedSource.Retrieve(5));

            Assert.AreEqual(6, _cacheMisses, "There should be 6 cache misses so far");

            // Since element 1 should be out of cache, a call to it should increase the misses by one.
            Assert.AreEqual(MockedResult(1), cachedSource.Retrieve(1), "Wrong element was returned from cache!");

            Assert.AreEqual(7, _cacheMisses, "There should be 7 cache misses so far");
        }

        [Description("CachingWrapper tester")]
        [TestMethod]
        public void TestClearCache()
        {
            _cacheMisses = 0;

            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 3);

            cachedSource.Retrieve(0);
            cachedSource.Retrieve(1);
            cachedSource.Retrieve(2);

            var cleared = cachedSource.ClearCache(Timeout.Infinite);

            Assert.IsTrue(cleared, "ClearCache should return true when the cache is cleared.");

            int missesBefore = _cacheMisses;

            cachedSource.Retrieve(0);

            Assert.AreEqual(missesBefore + 1, _cacheMisses, "Cache miss count should increment after clearing the cache.");
        }

        [Description("CachingWrapper tester")]
        [TestMethod]
        public void TestCachingWrapperMissRatio()
        {
            const int iterations = 100;

            Console.WriteLine("Full capacity:");

            MonteCarloSim(iterations, 0);

            Console.WriteLine("");
            Console.WriteLine("Partial capacity (LRU)");

            MonteCarloSim(iterations, 1);
            MonteCarloSim(iterations, 2);
            MonteCarloSim(iterations, 4);
            MonteCarloSim(iterations, 8);
            MonteCarloSim(iterations, 16);
            MonteCarloSim(iterations, 32);
            MonteCarloSim(iterations, 64);
        }

        private void MonteCarloSim(int requestIterations, int divisor)
        {
            var global = new IterationResults();

            for (int i = 0; i < 100; i++)
            {
                var result = IterativeTest(requestIterations, divisor);
                global.MissRatio = (global.MissRatio + result.MissRatio) / 2;
                global.Elapsed = (global.Elapsed + result.Elapsed) / 2;

                if (i == 0 || result.MissRatio > global.MaxMissRatio)
                {
                    global.MaxMissRatio = result.MissRatio;
                }

                if (i == 0 || result.MissRatio < global.MinMissRatio)
                {
                    global.MinMissRatio = result.MissRatio;
                }

                if (i == 0 || result.Elapsed > global.MaxElapsed)
                {
                    global.MaxElapsed = result.Elapsed;
                }

                if (i == 0 || result.Elapsed < global.MinElapsed)
                {
                    global.MinElapsed = result.Elapsed;
                }
            }

            int capacity = (divisor == 0) ? 0 : (requestIterations / divisor);

            Console.WriteLine(
                    "Iterations : {0:00000}, Capacity: {1:00000} (1/{2:00}), Hit ratio: {3:00.00}% ({4:00.00}% - {5:00.00}%), Elapsed: {6:0000.00}ms ({7:0000.00}ms - {8:0000.00}ms)",
                    requestIterations, capacity, divisor, 100 - global.MissRatio, 100 - global.MaxMissRatio, 100 - global.MinMissRatio,
                    global.Elapsed, global.MinElapsed, global.MaxElapsed);
        }

        private IterationResults IterativeTest(int iterations, int capacityDivisor)
        {
            var stopwatch = new Stopwatch();

            _cacheMisses = 0;

            int capacity = (capacityDivisor == 0) ? 0 : (iterations / capacityDivisor);

            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, capacity);

            stopwatch.Start();
            var random = new Random();
            for (int i = 0; i < iterations - 1; i++)
            {
                int num = random.Next(1, iterations);

                cachedSource.Retrieve(num);
            }
            stopwatch.Stop();

            decimal missRatio = (100 / (decimal)iterations) * _cacheMisses;

            return new IterationResults {MissRatio = missRatio, Elapsed = stopwatch.ElapsedMilliseconds};

            //Console.WriteLine("Iterations : {0:0000}, Capacity: {1:0000} (1/{2}), Cache misses: {3:0000}, Miss ratio: {4:00.00}% - Hit ratio: {5:00.00}%, Elapsed: {6}ms", iterations, capacity, capacityDivisor, _cacheMisses, MissRatio, 100 - MissRatio, stopwatch.ElapsedMilliseconds);
        }

        private string DatabaseCallMock(int key)
        {
            Interlocked.Increment(ref _cacheMisses);
            _onCacheMiss?.Invoke(key); // Invoke custom action on cache miss

            if (_customDatabaseCallMock != null)
            {
                return _customDatabaseCallMock(key);
            }

            // simulate some overhead
            int work = 1;
            for (int i = 1; i < 10000; i++)
            {
                work = work * i;
            }

            return MockedResult(key);
        }

        private static string MockedResult(int key)
        {
            return "key: " + key;
        }

        #region Concurrency Tests

        [TestMethod]
        public void TestConcurrency_MultipleThreads_DifferentKeys_MixInitialMissAndHit()
        {
            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 10);
            int initialItemsToCache = 5;
            int expectedMisses = 0;

            // Pre-populate some items
            for (int i = 0; i < initialItemsToCache; i++)
            {
                cachedSource.Retrieve(i);
                expectedMisses++;
            }
            Assert.AreEqual(expectedMisses, _cacheMisses, "Initial cache population misses incorrect.");

            int taskCount = 20;
            int operationsPerTask = 10;
            var tasks = new List<Task<List<string>>>();
            var keyToRetrieveCounts = new ConcurrentDictionary<int, int>();

            Action<int> countKeyRetrieval = (key) => keyToRetrieveCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
            _onCacheMiss = countKeyRetrieval; // Use _onCacheMiss to track calls to DatabaseCallMock indirectly

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var results = new List<string>();
                    var random = new Random(Thread.CurrentThread.ManagedThreadId + i);
                    for (int j = 0; j < operationsPerTask; j++)
                    {
                        // Half retrieve existing keys, half retrieve new keys
                        int key = (j % 2 == 0) ? random.Next(0, initialItemsToCache) : random.Next(initialItemsToCache, initialItemsToCache + operationsPerTask / 2);
                        results.Add(cachedSource.Retrieve(key));
                    }
                    return results;
                }));
            }

            Task.WhenAll(tasks).GetAwaiter().GetResult();

            // Calculate expected misses after concurrent operations
            // Initial misses are already counted.
            // New keys are from initialItemsToCache up to initialItemsToCache + operationsPerTask / 2 - 1
            // Each of these new keys should be missed once.
            int uniqueNewKeys = operationsPerTask / 2;
            expectedMisses += uniqueNewKeys;

            // Verify actual misses using the _onCacheMiss counter
            // This counts how many times DatabaseCallMock was actually invoked for distinct keys during concurrent phase.
            // This assertion is tricky because multiple threads might try to get the same *new* key.
            // The _cacheMisses field is a global counter of any call to DatabaseCallMock.
            // Let's use the _cacheMisses field directly.
            Assert.AreEqual(expectedMisses, _cacheMisses, "Total cache misses incorrect after concurrent access.");

            foreach (var task in tasks)
            {
                foreach (var resultString in task.Result)
                {
                    Assert.IsTrue(resultString.StartsWith("key: "), "Result string format incorrect.");
                }
            }
        }

        [TestMethod]
        public void TestConcurrency_MultipleThreads_SameKey_InitialMiss()
        {
            int simultaneousThreads = 10;
            int keyToRetrieve = 100; // A new key not in cache
            var barrier = new Barrier(simultaneousThreads);
            var tasks = new List<Task<string>>();

            // Set up _onCacheMiss to count invocations for the specific key
            var specificKeyMissCount = 0;
            _onCacheMiss = (key) => {
                if (key == keyToRetrieve) Interlocked.Increment(ref specificKeyMissCount);
            };

            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 5);

            for (int i = 0; i < simultaneousThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    barrier.SignalAndWait(); // Synchronize threads to hit Retrieve nearly simultaneously
                    return cachedSource.Retrieve(keyToRetrieve);
                }));
            }

            Task.WhenAll(tasks).GetAwaiter().GetResult();

            // Assertions
            Assert.AreEqual(1, specificKeyMissCount, "RetrieveFromOriginalSource should have been called only once for the highly contended key.");
            Assert.AreEqual(1, _cacheMisses, "Overall cache misses should be 1."); // Global _cacheMisses check

            foreach (var task in tasks)
            {
                Assert.AreEqual(MockedResult(keyToRetrieve), task.Result, "Thread returned incorrect value.");
            }
        }

        [TestMethod]
        public void TestConcurrency_RetrieveAndClearCache()
        {
            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 20);
            int retrieveTaskCount = 10;
            int clearTaskCount = 3;
            int operationsPerRetrieveTask = 100;
            CancellationTokenSource cts = new CancellationTokenSource();
            var tasks = new List<Task>();

            // Retrieve tasks
            for (int i = 0; i < retrieveTaskCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var random = new Random(Thread.CurrentThread.ManagedThreadId + i);
                    for (int j = 0; j < operationsPerRetrieveTask && !cts.IsCancellationRequested; j++)
                    {
                        try
                        {
                            cachedSource.Retrieve(random.Next(0, 50)); // Retrieve various keys
                        }
                        catch (Exception ex) // Catch potential exceptions during stress test
                        {
                            Assert.Fail($"Retrieve operation failed with: {ex.Message}");
                        }
                    }
                }, cts.Token));
            }

            // ClearCache tasks
            for (int i = 0; i < clearTaskCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    // Let retrieve tasks run a bit
                    Thread.Sleep(random.Next(50, 150)); // Stagger clear operations
                    try
                    {
                        cachedSource.ClearCache();
                    }
                    catch (Exception ex)
                    {
                         Assert.Fail($"ClearCache operation failed with: {ex.Message}");
                    }
                }));
            }

            // Let tasks run for a short period
            bool allTasksCompleted = Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10)); // Wait for tasks with timeout

            if (!allTasksCompleted)
            {
                cts.Cancel(); // Request cancellation if not all tasks completed
                // Optionally, give a little more time for tasks to acknowledge cancellation
                Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(2));
            }

            // Primary assertion is that no deadlocks or unhandled exceptions occurred.
            // If tasks did not complete in time, it might indicate an issue (though could also be slow test runner).
            // We check task statuses to be more specific if possible (though WaitAll timeout doesn't give per-task status easily here)
            foreach(var task in tasks)
            {
                Assert.IsTrue(task.IsCompleted || task.IsCanceled, $"Task {task.Id} did not complete. Status: {task.Status}");
                 if (task.IsFaulted) Assert.Fail($"Task {task.Id} faulted: {task.Exception}");
            }
            Assert.IsTrue(allTasksCompleted, "Not all concurrent retrieve/clear tasks completed within the timeout, potential deadlock or excessive contention.");
        }

        // Helper for timeout tests to hold a lock
        private static void HoldLock(ReaderWriterLockSlim rwl, bool writeLock, int milliseconds)
        {
            if (writeLock)
            {
                if (rwl.TryEnterWriteLock(TimeSpan.FromMilliseconds(milliseconds + 100))) // Try to get it for longer than hold
                {
                    Thread.Sleep(milliseconds);
                    rwl.ExitWriteLock();
                }
            }
            else
            {
                if (rwl.TryEnterReadLock(TimeSpan.FromMilliseconds(milliseconds + 100)))
                {
                    Thread.Sleep(milliseconds);
                    rwl.ExitReadLock();
                }
            }
        }

        private static Random random = new Random(); // Used by TestConcurrency_RetrieveAndClearCache if not shadowed

        #endregion

        #region Timeout Tests

        // Helper to get the internal ReaderWriterLockSlim for advanced test scenarios
        private ReaderWriterLockSlim GetLockManager(CachingWrapper<int, string> cache)
        {
            // Using reflection to access the private _lockManager field
            var fieldInfo = typeof(CachingWrapper<int, string>).GetField("_lockManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldInfo == null)
            {
                Assert.Fail("_lockManager field not found. Reflection setup failed.");
            }
            return (ReaderWriterLockSlim)fieldInfo.GetValue(cache)!;
        }


        [TestMethod]
        public void TestTimeout_Retrieve_ReadLockTimesOut_FetchesFromSource()
        {
            _cacheMisses = 0;
            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 5);
            var lockManager = GetLockManager(cachedSource);

            int keyToRetrieve = 1;
            int lockHoldTime = 200;
            int retrieveTimeout = 50; // Shorter than lock hold time

            // Hold a write lock to block TryEnterUpgradeableReadLock
            var lockHolderTask = Task.Run(() => HoldLock(lockManager, true, lockHoldTime));
            Thread.Sleep(50); // Give lockHolderTask a chance to acquire the lock before proceeding

            string? value = null;
            Exception? ex = null;
            try
            {
                 value = cachedSource.Retrieve(keyToRetrieve, retrieveTimeout);
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.IsNull(ex, "Retrieve should not throw an exception on read lock timeout, but fetch from source.");
            Assert.AreEqual(MockedResult(keyToRetrieve), value, "Value retrieved was not the expected one from original source.");
            Assert.AreEqual(1, _cacheMisses, "DatabaseCallMock should have been called once.");

            // Verify cache state: item should NOT be in cache if WriteToCache also timed out or wasn't reached effectively
            // This part is tricky: FetchFromOriginalSource is called, then WriteToCache.
            // If the initial TryEnterUpgradeableReadLock timed out, the _lockManager is free when FetchFromOriginalSource -> WriteToCache happens.
            // So it *should* cache it.
            _cacheMisses = 0; // Reset for verification
            value = cachedSource.Retrieve(keyToRetrieve); // No timeout, should be a cache hit now
            Assert.AreEqual(0, _cacheMisses, "Item should have been cached after the initial fetch.");
        }

        [TestMethod]
        public void TestTimeout_Retrieve_WriteLockTimesOutDuringCacheUpdate()
        {
            _cacheMisses = 0;
            int keyToRetrieve = 201; // New key
            // int lockHoldTime = 200; // Unused variable removed
            // int retrieveTimeout = 50; // Unused variable removed

            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 5);
            var lockManager = GetLockManager(cachedSource);

            int writeLockHoldDuration = 200; // How long the interfering task will hold the write lock
            int retrieveInternalTimeout = 50;  // Timeout for WriteToCache's attempt to get the lock

            Task? interferingLockTask = null;
            _customDatabaseCallMock = (key) => {
                // This is called after CachingWrapper has an upgradeable read lock (or no lock if initial read lock timed out).
                // Start a task that will quickly grab and hold the write lock.
                interferingLockTask = Task.Run(() => HoldLock(lockManager, true, writeLockHoldDuration));
                Thread.Sleep(30); // Give it a moment to start and acquire the write lock
                return MockedResult(key);
            };

            // Retrieve will call _customDatabaseCallMock, which starts the interferingLockTask.
            // Then Retrieve will call WriteToCache, which will attempt to get a write lock with 'retrieveInternalTimeout'.
            // This attempt should time out because interferingLockTask holds the write lock.
            string value = cachedSource.Retrieve(keyToRetrieve, retrieveInternalTimeout);

            Assert.AreEqual(MockedResult(keyToRetrieve), value, "Value from original source should be returned.");
            Assert.IsNotNull(interferingLockTask, "Interfering lock task should have been started.");
            // Wait for the interfering lock task to complete to avoid issues with subsequent tests or cleanup.
            interferingLockTask?.Wait(writeLockHoldDuration + 100);
            Assert.AreEqual(1, _cacheMisses, "DatabaseCallMock should have been called once.");

            // Now, verify the item was NOT cached because WriteToCache should have timed out
            _cacheMisses = 0; // Reset miss counter
            cachedSource.Retrieve(keyToRetrieve);
            Assert.AreEqual(1, _cacheMisses, "Item should NOT have been cached due to WriteToCache timeout.");
        }


        [TestMethod]
        public void TestTimeout_ClearCache_WriteLockTimesOut()
        {
            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 5);
            var lockManager = GetLockManager(cachedSource);

            // Pre-populate
            cachedSource.Retrieve(1);
            cachedSource.Retrieve(2);
            _cacheMisses = 0;

            int lockHoldTime = 200;
            int clearCacheTimeout = 50;

            // Hold a read lock (or write lock) to block ClearCache's write lock attempt
            var lockHolderTaskClear = Task.Run(() => HoldLock(lockManager, false, lockHoldTime)); // Use false for readLock to block writeLock
            Thread.Sleep(50); // Give lockHolderTaskClear a chance to acquire the lock

            bool cleared = cachedSource.ClearCache(clearCacheTimeout);

            Assert.IsFalse(cleared, "ClearCache should return false if it times out.");
            lockHolderTaskClear.Wait(lockHoldTime + 100); // Ensure the task finishes to release the lock

            // Verify cache was not cleared
            cachedSource.Retrieve(1);
            Assert.AreEqual(0, _cacheMisses, "Cache should not have been cleared (item 1 still a hit).");
            cachedSource.Retrieve(2);
            Assert.AreEqual(0, _cacheMisses, "Cache should not have been cleared (item 2 still a hit).");
        }

        #endregion

        #region Exception Handling Tests

        [TestMethod]
        public void TestException_RetrieveFromOriginalSource_ThrowsException()
        {
            _cacheMisses = 0;
            int keyToRetrieve = 301;
            var expectedException = new InvalidOperationException("Failed to retrieve from source");

            _customDatabaseCallMock = key => throw expectedException;

            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 5);

            Exception? thrownException = null;
            try
            {
                cachedSource.Retrieve(keyToRetrieve);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            Assert.IsNotNull(thrownException, "Exception was expected from RetrieveFromOriginalSource.");
            Assert.AreSame(expectedException, thrownException, "The specific exception thrown by RetrieveFromOriginalSource was not propagated.");
            Assert.AreEqual(1, _cacheMisses, "DatabaseCallMock should have been called once (and thrown).");

            // Verify item is not in cache
            _cacheMisses = 0; // Reset for verification
            _customDatabaseCallMock = null; // Remove throwing mock

            cachedSource.Retrieve(keyToRetrieve); // Should be a miss again
            Assert.AreEqual(1, _cacheMisses, "Item should not be in cache after original source threw an exception.");
        }

        #endregion

        #region Force Refresh Tests

        [TestMethod]
        public void TestForceRefresh_RetrievesFromSource_UpdatesCache()
        {
            _cacheMisses = 0;
            int keyToRefresh = 401;
            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 5);

            // Initial retrieval - cache miss
            string? initialValue = cachedSource.Retrieve(keyToRefresh);
            Assert.AreEqual(1, _cacheMisses, "Initial retrieval should be a cache miss.");
            Assert.AreEqual(MockedResult(keyToRefresh), initialValue);

            // Setup a new mock result for the forced refresh
            string refreshedMockSuffix = "_refreshed";
            _customDatabaseCallMock = key => MockedResult(key) + refreshedMockSuffix;

            // Force refresh
            string? refreshedValue = cachedSource.Retrieve(keyToRefresh, forceFresh: true);
            Assert.AreEqual(2, _cacheMisses, "Force refresh should be a cache miss.");
            Assert.AreEqual(MockedResult(keyToRefresh) + refreshedMockSuffix, refreshedValue, "Refreshed value is incorrect.");

            // Retrieve again, should be a cache hit with the refreshed value
            _customDatabaseCallMock = null; // Clear custom mock
            string? valueAfterRefresh = cachedSource.Retrieve(keyToRefresh);
            Assert.AreEqual(2, _cacheMisses, "Retrieval after refresh should be a cache hit.");
            Assert.AreEqual(MockedResult(keyToRefresh) + refreshedMockSuffix, valueAfterRefresh, "Value after refresh from cache is incorrect.");
        }

        #endregion

        #region Zero Capacity Tests

        [TestMethod]
        public void TestZeroCapacity_ItemsCachedIndefinitely()
        {
            _cacheMisses = 0;
            // Capacity 0 means LRU list is null, items are cached in _localCache indefinitely
            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 0);

            int numItems = 10;
            for (int i = 0; i < numItems; i++)
            {
                cachedSource.Retrieve(i);
                Assert.AreEqual(i + 1, _cacheMisses, $"Miss count should be {i + 1} after retrieving key {i}.");
            }

            // All items should be hits now
            for (int i = 0; i < numItems; i++)
            {
                cachedSource.Retrieve(i);
                Assert.AreEqual(numItems, _cacheMisses, $"Miss count should remain {numItems} when retrieving key {i} again.");
            }
        }

        [TestMethod]
        public void TestZeroCapacity_ClearCache()
        {
            _cacheMisses = 0;
            var cachedSource = new CachingWrapper<int, string>(DatabaseCallMock, 0);

            cachedSource.Retrieve(1);
            cachedSource.Retrieve(2);
            Assert.AreEqual(2, _cacheMisses);

            cachedSource.ClearCache();
            _cacheMisses = 0; // Reset after clear for verification

            cachedSource.Retrieve(1);
            Assert.AreEqual(1, _cacheMisses, "Key 1 should be a miss after ClearCache.");
            cachedSource.Retrieve(2);
            Assert.AreEqual(2, _cacheMisses, "Key 2 should be a miss after ClearCache.");
        }

        #endregion
    }
}