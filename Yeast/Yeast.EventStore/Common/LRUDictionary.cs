using System;
using System.Collections.Generic;

using System.Text;

namespace Yeast.EventStore.Common
{
	public class LRUDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	{
		public static double DefaultCapacityReduction = 0.9;
		public event EventHandler<KeyValueRemovedArgs<TKey, TValue>> Removed;

		private int _capacity;
		public int Capacity { get { return _capacity; } }
		private class LValue<TKey, TValue>
		{
			public TKey Key;
			public TValue Value;
			public bool Deleted;
		}
		private class DValue<TValue>
		{
			public TValue Value;
			public LinkedListNode<LValue<TKey, TValue>> Node;
		}
		private Dictionary<TKey, DValue<TValue>> _dictionary;
		private LinkedList<LValue<TKey, TValue>> _linkedList;

		public LRUDictionary(int capacity)
		{
			if (1 > capacity)
			{
				throw new ArgumentOutOfRangeException("capacity", capacity, "Capacity cannot be less than 1.");
			}

			_capacity = capacity;
			_dictionary = new Dictionary<TKey, DValue<TValue>>(capacity);
			_linkedList = new LinkedList<LValue<TKey, TValue>>();
		}

		private void _Add(TKey key, TValue value, bool throwIfContains)
		{
			DValue<TValue> dValue;
			lock (_dictionary)
			{
				if (throwIfContains)
				{
					dValue = new DValue<TValue>() { Value = value };
					_dictionary.Add(key, dValue);
				}
				else
				{
					if (!_dictionary.TryGetValue(key, out dValue))
					{
						dValue = new DValue<TValue>() { Value = value };
					}
					_dictionary[key] = dValue;
				}
			}

			UpdateLRU(key, dValue);
		}

		private bool _Remove(TKey key)
		{
			bool b = false;
			DValue<TValue> value;
			lock (_dictionary)
			{
				_dictionary.TryGetValue(key, out value);
				if (b = _dictionary.Remove(key))
				{
					value.Node.Value.Deleted = b;
					_OnRemoved(key, value.Value);
				}
			}
			return b;
		}

		private bool _TryGetValue(TKey key, out TValue value, bool throwIfNotExists)
		{
			value = default(TValue);

			DValue<TValue> dValue;
			if (_dictionary.TryGetValue(key, out dValue))
			{
				value = dValue.Value;
				UpdateLRU(key, dValue);
				return true;
			}
			else if (throwIfNotExists)
			{
				throw new KeyNotFoundException();
			}

			return false;
		}

		private void _Clear()
		{
			List<TKey> keys;
			lock (_dictionary)
			{
				keys = new List<TKey>(_dictionary.Keys);
			}
			foreach (var key in keys)
			{
				_Remove(key);
			}
		}

		private void _OnRemoved(TKey key, TValue value)
		{
			if (null != Removed)
			{
				Removed(this, new KeyValueRemovedArgs<TKey, TValue>() { Key = key, Value = value });
			}
		}

		private void UpdateLRU(TKey key, DValue<TValue> dValue)
		{
			lock (dValue)
			{
				var oldNode = dValue.Node;
				if (null != oldNode)
				{
					oldNode.Value.Deleted = true;
				}
				dValue.Node = new LinkedListNode<LValue<TKey,TValue>>(new LValue<TKey, TValue>() { Key = key, Value = dValue.Value });
				lock (_linkedList)
				{
					_linkedList.AddLast(dValue.Node);
				}
			}

			if (_dictionary.Count > _capacity)
			{
				var removedItems = new List<LValue<TKey, TValue>>();
				var targetCapacity = (int)(_capacity * DefaultCapacityReduction);
				while (_dictionary.Count > targetCapacity)
				{
					LValue<TKey, TValue> first;
					lock (_linkedList)
					{
						first = _linkedList.First.Value;
						_linkedList.RemoveFirst();
					}
					if (first.Deleted)
					{
						continue;
					}

					lock (_dictionary)
					{
						if (_dictionary.Remove(first.Key))
						{
							removedItems.Add(first);
						}
					}
				}

				foreach (var item in removedItems)
				{
					_OnRemoved(item.Key, item.Value);
				}
			}
		}

		public void Add(TKey key, TValue value)
		{
			_Add(key, value, true);
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
			return _Remove(key);
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			return _TryGetValue(key, out value, false);
		}

		public ICollection<TValue> Values
		{
			get { throw new NotImplementedException(); }
		}

		public TValue this[TKey key]
		{
			get
			{
				TValue value;
				_TryGetValue(key, out value, true);
				return value;
			}
			set
			{
				_Add(key, value, false);
			}
		}

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			_Add(item.Key, item.Value, true);
		}

		public void Clear()
		{
			_Clear();
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
			return _Remove(item.Key);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			List<KeyValuePair<TKey, TValue>> list = new List<KeyValuePair<TKey, TValue>>();
			lock (_dictionary)
			{
				foreach (var item in _dictionary)
				{
					list.Add(new KeyValuePair<TKey,TValue>(item.Key, item.Value.Value));
				}
			}
			return list.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return _dictionary.GetEnumerator();
		}
	}
}
