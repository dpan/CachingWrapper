using System.Collections.Generic;

namespace Utilities
{
    /// <summary>
    /// Indexed Linked List, primarily used in the <see cref="CachingWrapper{TKey,TValue}"/>. This class is NOT threadsafe.
    /// </summary>
    /// <typeparam name="TKey">Type of Key</typeparam>
    internal class IndexedLinkedList<TKey>
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

        public TKey First
        {
            get { return _data.First.Value; }
        }

        public void Add(TKey value)
        {
            _index[value] = _data.AddLast(value);
        }

        public void RemoveFirst()
        {
            _index.Remove(_data.First.Value);
            _data.RemoveFirst();
        }

        public void Remove(TKey value)
        {
            LinkedListNode<TKey> node;
            if (_index.TryGetValue(value, out node))
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