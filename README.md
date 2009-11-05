# Caching Wrapper

A thread safe wrapper around a db or service call to provide ["Least Recently Used" (LRU)](http://en.wikipedia.org/wiki/Cache_algorithms) Caching.
     
## Usage
                                                            
Initialize the wrapper. You can use any type for the key and the value.

In the following example a cache capacity of 100 is chosen. Use 0 to disable LRU caching.      
		
    var cachedSource = new CachingWrapper<int, string>(GetFromOriginalSource, 100);     

    cachedSource.Retrieve(10); // Next time this key is requested, it'll be retrieved from the cache

Method used as a delegate to retrieve value from original source:

    private string GetFromOriginalSource(int key)
    {
        //  Retrieve value for the specific key from the database, a web service, a file or any other source
        return "result";
    }