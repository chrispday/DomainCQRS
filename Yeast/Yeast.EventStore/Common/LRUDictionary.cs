using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Common
{
	public class LRUDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	{
		private int _capacity = int.MaxValue;
		public int Capacity
		{
			get { return _capacity; }
		}

		private Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
		private LinkedList<TKey> linkedList = new LinkedList<TKey>();

		public event EventHandler<EventArgs> Removed;

		public LRUDictionary() : base() { }
		public LRUDictionary(int capacity) : base() { _capacity = capacity; }

		private void UpdateLRU(TKey key, bool contains)
		{
			if (contains)
			{
				linkedList.Remove(key);
			}
			linkedList.AddFirst(key);

			while (linkedList.Count > _capacity)
			{
				var lastKey = linkedList.Last.Value;
				var lastValue = dictionary[lastKey];
				dictionary.Remove(lastKey);
				linkedList.Remove(lastKey);
				if (null != Removed)
				{
					Removed(new KeyValuePair<TKey, TValue>(lastKey, lastValue), new EventArgs());
				}
			}
		}

		public void Add(TKey key, TValue value)
		{
			dictionary.Add(key, value);
			UpdateLRU(key, false);
		}

		public bool ContainsKey(TKey key)
		{
			return dictionary.ContainsKey(key);
		}

		public ICollection<TKey> Keys
		{
			get { return dictionary.Keys; }
		}

		public bool Remove(TKey key)
		{
			var b = dictionary.Remove(key);
			if (b)
			{
				linkedList.Remove(key);
			}
			return b;
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			var b = dictionary.TryGetValue(key, out value);
			if (b)
			{
				UpdateLRU(key, true);
			}
			return b;
		}

		public ICollection<TValue> Values
		{
			get { return dictionary.Values; }
		}

		public TValue this[TKey key]
		{
			get
			{
				var t = dictionary[key];
				UpdateLRU(key, true);
				return t;
			}
			set
			{
				var contains = dictionary.ContainsKey(key);
				dictionary[key] = value;
				UpdateLRU(key, contains);
			}
		}

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			Add(item.Key, item.Value);
		}

		public void Clear()
		{
			dictionary.Clear();
			linkedList.Clear();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return dictionary.Contains(item);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			((IDictionary<TKey, TValue>) dictionary).CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get { return dictionary.Count; }
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			var b = ((IDictionary<TKey, TValue>)dictionary).Remove(item);
			if (b)
			{
				linkedList.Remove(item.Key);
			}
			return b;
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return dictionary.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return dictionary.GetEnumerator();
		}
	}
}
