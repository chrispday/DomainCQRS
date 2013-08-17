using System;
using System.Collections.Generic;

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
			lock (_linkedList)
			{
				if (contains)
				{
					_linkedList.Remove(key);
				}
				_linkedList.AddFirst(key);
			}

			if (_linkedList.Count > _capacity)
			{
				List<KeyValuePair<TKey, TValue>> removedItems = new List<KeyValuePair<TKey, TValue>>();
				var targetCapacity = (int)(_capacity * DefaultCapacityReduction);
				while (_linkedList.Count > targetCapacity)
				{
					TKey lastKey;
					lock (_linkedList)
					{
						lastKey = _linkedList.Last.Value;
						_linkedList.Remove(lastKey);
					}
					lock (_dictionary)
					{
						TValue lastValue;
						_dictionary.TryGetValue(lastKey, out lastValue);
						_dictionary.Remove(lastKey);
						removedItems.Add(new KeyValuePair<TKey, TValue>(lastKey, lastValue));
					}
				}
				if (null != Removed)
				{
					foreach (var item in removedItems)
					{
						Removed(this, new KeyValueRemovedArgs<TKey, TValue>() { Key = item.Key, Value = item.Value});
					}
				}
			}
		}

		public void Add(TKey key, TValue value)
		{
			lock (_dictionary)
			{
				_dictionary.Add(key, value);
			}
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
			bool b = false;
			TValue value;
			lock (_dictionary)
			{
				_dictionary.TryGetValue(key, out value);
				b = _dictionary.Remove(key);
			}
			if (b)
			{
				lock (_linkedList)
				{
					_linkedList.Remove(key);
				}
				if (null != Removed)
				{
					Removed(this, new KeyValueRemovedArgs<TKey, TValue>() { Key = key, Value = value });
				}
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
				TValue t;
				lock (_dictionary)
				{
					t = _dictionary[key];
				}
				UpdateLRU(key, true);
				return t;
			}
			set
			{
				bool contains;
				lock (_dictionary)
				{
					contains = _dictionary.ContainsKey(key);
					_dictionary[key] = value;
				}
				UpdateLRU(key, contains);
			}
		}

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			Add(item.Key, item.Value);
		}

		public void Clear()
		{
			List<KeyValuePair<TKey, TValue>> items = new List<KeyValuePair<TKey, TValue>>();
			lock (_dictionary)
			{
				foreach (var kvp in _dictionary)
				{
					items.Add(kvp);
				}
				_dictionary.Clear();
			}
			lock (_linkedList)
			{
				_linkedList.Clear();
			}
			if (null != Removed)
			{
				foreach (var item in items)
				{
					Removed(this, new KeyValueRemovedArgs<TKey, TValue>() { Key = item.Key, Value = item.Value });
				}
			}
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return (_dictionary as IDictionary<TKey, TValue>).Contains(item);
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
			return Remove(item.Key);
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
