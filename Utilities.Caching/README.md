# Utilities.Caching

A thread-safe LRU caching library for .NET.

## Features

*   Thread-safe access to cached items.
*   Least Recently Used (LRU) eviction policy.
*   Configurable cache capacity.
*   Delegate for custom data retrieval.
*   Supports .NET 9.

## Usage

```csharp
// Example: Cache results of a string lookup
var cache = new CachingWrapper<int, string>(
    key => {
        // Simulate fetching data from a database or service
        Console.WriteLine($"Cache miss for key: {key}");
        System.Threading.Thread.Sleep(100); // Simulate delay
        return $"Value for {key}";
    },
    capacity: 100 // Cache up to 100 items
);

string value1 = cache.Retrieve(1);
string value2 = cache.Retrieve(1); // Will be fetched from cache
```
