using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Common
{
	public class LRUDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	{
		public static double DefaultCapacityReduction = 0.9;
		public event EventHandler<KeyValueRemovedArgs<TKey, TValue>> Removed;

		private int _capacity = int.MaxValue;
		public int Capacity { get { return _capacity; } }
		private Dictionary<TKey, TValue> _dictionary;
		private LinkedList<TKey> _linkedList;

		public LRUDictionary() : base() 
		{
			_dictionary = new Dictionary<TKey, TValue>();
			_linkedList = new LinkedList<TKey>();
		}

		public LRUDictionary(int capacity)
		{
			if (1 > capacity)
			{
				throw new ArgumentOutOfRangeException("capacity", capacity, "Capacity cannot be less than 1.");
			}

			_capacity = capacity;
			_dictionary = new Dictionary<TKey, TValue>(capacity);
			_linkedList = new LinkedList<TKey>();
		}

		private void UpdateLRU(TKey key, bool contains)
		{
			if (contains)
			{
				_linkedList.Remove(key);
			}
			_linkedList.AddFirst(key);

			if (_linkedList.Count > _capacity)
			{
				var targetCapacity = (int)(_capacity * DefaultCapacityReduction);
				while (_linkedList.Count > targetCapacity)
				{
					var lastKey = _linkedList.Last.Value;
					var lastValue = _dictionary[lastKey];
					_dictionary.Remove(lastKey);
					_linkedList.Remove(lastKey);
					if (null != Removed)
					{
						Removed(this, new KeyValueRemovedArgs<TKey, TValue>() { Key = lastKey, Value = lastValue });
					}
				}
			}
		}

		public void Add(TKey key, TValue value)
		{
			_dictionary.Add(key, value);
			UpdateLRU(key, false);
		}

		public bool ContainsKey(TKey key)
		{
			return _dictionary.ContainsKey(key);
		}

		public ICollection<TKey> Keys
		{
			get { return _dictionary.Keys; }
		}

		public bool Remove(TKey key)
		{
			var b = _dictionary.Remove(key);
			if (b)
			{
				_linkedList.Remove(key);
			}
			return b;
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			var b = _dictionary.TryGetValue(key, out value);
			if (b)
			{
				UpdateLRU(key, true);
			}
			return b;
		}

		public ICollection<TValue> Values
		{
			get { return _dictionary.Values; }
		}

		public TValue this[TKey key]
		{
			get
			{
				var t = _dictionary[key];
				UpdateLRU(key, true);
				return t;
			}
			set
			{
				var contains = _dictionary.ContainsKey(key);
				_dictionary[key] = value;
				UpdateLRU(key, contains);
			}
		}

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			Add(item.Key, item.Value);
		}

		public void Clear()
		{
			_dictionary.Clear();
			_linkedList.Clear();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return _dictionary.Contains(item);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			((IDictionary<TKey, TValue>) _dictionary).CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return _dictionary.Count; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			var b = ((IDictionary<TKey, TValue>)_dictionary).Remove(item);
			if (b)
			{
				_linkedList.Remove(item.Key);
				if (null != Removed)
				{
					Removed(this, new KeyValueRemovedArgs<TKey, TValue>() { Key = item.Key, Value = item.Value });
				}
			}
			return b;
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return _dictionary.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return _dictionary.GetEnumerator();
		}
	}
}
