namespace ProcessLens.App;

/// <summary>A small, thread-safe LRU cache. Eviction drops only cached references, never source data.</summary>
internal sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _entries;
    private readonly LinkedList<(TKey Key, TValue Value)> _recent = new();

    public BoundedCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _entries = new Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>>(comparer);
    }

    public int Count { get { lock (_gate) return _entries.Count; } }
    public TKey[] Keys { get { lock (_gate) return _entries.Keys.ToArray(); } }

    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var node)) { value = default!; return false; }
            _recent.Remove(node);
            _recent.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
    }

    public TValue this[TKey key]
    {
        set
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(key, out var node))
                {
                    node.Value = (key, value);
                    _recent.Remove(node);
                    _recent.AddFirst(node);
                    return;
                }
                if (_entries.Count == _capacity)
                {
                    var oldest = _recent.Last!;
                    _entries.Remove(oldest.Value.Key);
                    _recent.RemoveLast();
                }
                var added = _recent.AddFirst((key, value));
                _entries.Add(key, added);
            }
        }
    }

    public bool Remove(TKey key)
    {
        lock (_gate)
        {
            if (!_entries.Remove(key, out var node)) return false;
            _recent.Remove(node);
            return true;
        }
    }
}
