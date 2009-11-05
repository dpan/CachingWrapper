using System;
using System.Diagnostics;

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

        #endregion

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
            _cacheMisses++;

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
    }
}