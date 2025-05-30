using System.Collections.Generic;

namespace Utilities
{
    /// <summary>
    /// Indexed Linked List, primarily used in the <see cref="CachingWrapper{TKey,TValue}"/>. This class is NOT threadsafe.
    /// </summary>
    /// <typeparam name="TKey">Type of Key. Must be non-nullable.</typeparam>
    internal class IndexedLinkedList<TKey> where TKey : notnull
    {
        #region Data Members

        private readonly LinkedList<TKey> _data;
        private readonly Dictionary<TKey, LinkedListNode<TKey>> _index;

        #endregion

        public IndexedLinkedList() : this(0)
        {
        }

        public IndexedLinkedList(int capacity)
        {
            _data = new LinkedList<TKey>();
            _index = capacity > 0
                            ? new Dictionary<TKey, LinkedListNode<TKey>>(capacity)
                            : new Dictionary<TKey, LinkedListNode<TKey>>();
        }

        // Returns TKey (non-nullable as TKey is notnull).
        // Throws InvalidOperationException if the list is empty, matching LinkedList<T>.First behavior.
        public TKey First
        {
            get { return _data.First!.Value; } // Using null-forgiving operator
        }

        public void Add(TKey value) // value is TKey (notnull)
        {
            _index[value] = _data.AddLast(value);
        }

        public void RemoveFirst()
        {
            // LinkedList<T>.First throws if empty, so does RemoveFirst().
            // If _data is not empty, _data.First.Value is valid and non-null.
            if (_data.Count > 0)
            {
                _index.Remove(_data.First!.Value); // Using null-forgiving operator
                _data.RemoveFirst();
            }
        }

        public void Remove(TKey value) // value is TKey (notnull)
        {
            // In NRT context, 'out' parameters are assumed to be possibly not set if method returns false.
            // So node should be treated as potentially null after TryGetValue.
            if (_index.TryGetValue(value, out LinkedListNode<TKey>? node) && node != null)
            {
                _data.Remove(node);
                _index.Remove(value);
            }
        }

        public void Clear()
        {
            _data.Clear();
            _index.Clear();
        }
    }
}