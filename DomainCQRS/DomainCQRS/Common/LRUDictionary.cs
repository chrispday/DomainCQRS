using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Common
{
	/// <summary>
	/// Provides a Least Recently Used dictionary.
	/// When items are added and the capacity is exceeded it will remove items.
	/// The number of items to keep is determined by the Capacity * DefaultCapacityReduction
	/// </summary>
	/// <typeparam name="TKey">The key</typeparam>
	/// <typeparam name="TValue">The value</typeparam>
	public class LRUDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	{
		/// <summary>
		/// The default percentage of items to keep when the capacity is exceeded.
		/// </summary>
		public static double DefaultCapacityReduction = 0.9;
		/// <summary>
		/// Be notified when an item is removed.
		/// </summary>
		public event EventHandler<KeyValueRemovedArgs<TKey, TValue>> Removed;

		private int _capacity;
		/// <summary>
		/// The capacity.
		/// </summary>
		public int Capacity { get { return _capacity; } }
		private class LValue<TTKey, TTValue>
		{
			public TTKey Key;
			public TTValue Value;
			public bool Deleted;
		}
		private class DValue<TTKey, TTValue>
		{
			public TTValue Value;
			public LinkedListNode<LValue<TTKey, TTValue>> Node;
		}
		private Dictionary<TKey, DValue<TKey, TValue>> _dictionary;
		private LinkedList<LValue<TKey, TValue>> _linkedList;

		/// <summary>
		/// Create an <see cref="LRUDictionary[Tkey,TValue]"/>
		/// </summary>
		/// <param name="capacity">The capacity that should not be exceeded.</param>
		public LRUDictionary(int capacity)
		{
			if (1 > capacity)
			{
				throw new ArgumentOutOfRangeException("capacity", capacity, "Capacity cannot be less than 1.");
			}

			_capacity = capacity;
			_dictionary = new Dictionary<TKey, DValue<TKey, TValue>>(capacity);
			_linkedList = new LinkedList<LValue<TKey, TValue>>();
		}

		private void _Add(TKey key, TValue value, bool throwIfContains)
		{
			DValue<TKey, TValue> dValue;
			lock (_dictionary)
			{
				if (throwIfContains)
				{
					dValue = new DValue<TKey, TValue>() { Value = value };
					_dictionary.Add(key, dValue);
				}
				else
				{
					if (!_dictionary.TryGetValue(key, out dValue))
					{
						dValue = new DValue<TKey, TValue>() { Value = value };
					}
					_dictionary[key] = dValue;
				}
			}

			UpdateLRU(key, dValue);
		}

		private bool _Remove(TKey key)
		{
			bool b = false;
			DValue<TKey, TValue> value;
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

			DValue<TKey, TValue> dValue;
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

		private void UpdateLRU(TKey key, DValue<TKey, TValue> dValue)
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
				ReduceCount();
			}
		}

		private void ReduceCount()
		{
			var removedItems = new List<LValue<TKey, TValue>>();
			var targetCapacity = (int)(_capacity * DefaultCapacityReduction);
			if (Capacity == targetCapacity)
			{
				targetCapacity = Capacity - 1;
			}
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

		/// <summary>
		/// Add an item.
		/// </summary>
		/// <param name="key">The key</param>
		/// <param name="value">The value</param>
		public void Add(TKey key, TValue value)
		{
			_Add(key, value, true);
		}

		/// <summary>
		/// Is the key in the dictionary
		/// </summary>
		/// <param name="key">The key</param>
		/// <returns>True if key exists in dictioney, else false.</returns>
		public bool ContainsKey(TKey key)
		{
			return _dictionary.ContainsKey(key);
		}

		/// <summary>
		/// Returns the keys.
		/// </summary>
		public ICollection<TKey> Keys
		{
			get { return _dictionary.Keys; }
		}

		/// <summary>
		/// Removes a key.
		/// </summary>
		/// <param name="key">The key</param>
		/// <returns>If the key was removed.</returns>
		public bool Remove(TKey key)
		{
			return _Remove(key);
		}

		/// <summary>
		/// Try to get the value using the key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value if it exists</param>
		/// <returns>If the key exists</returns>
		public bool TryGetValue(TKey key, out TValue value)
		{
			return _TryGetValue(key, out value, false);
		}

		/// <summary>
		/// Gets the values.
		/// </summary>
		public ICollection<TValue> Values
		{
			get { throw new NotImplementedException(); }
		}

		/// <summary>
		/// Gets or sets the value using the key
		/// </summary>
		/// <param name="key">The key</param>
		/// <returns>The value, will throw if the key does not exist</returns>
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

		/// <summary>
		/// Adds the item.
		/// </summary>
		/// <param name="item">The item to add.</param>
		public void Add(KeyValuePair<TKey, TValue> item)
		{
			_Add(item.Key, item.Value, true);
		}

		/// <summary>
		/// Empties the dictionary.
		/// </summary>
		public void Clear()
		{
			_Clear();
		}

		/// <summary>
		/// If the item exists.
		/// </summary>
		/// <param name="item">The item to look for.</param>
		/// <returns>True if the item exists, else false.</returns>
		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return (_dictionary as IDictionary<TKey, TValue>).Contains(item);
		}

		/// <summary>
		/// Copies contents to an array
		/// </summary>
		/// <param name="array">The array to copy to</param>
		/// <param name="arrayIndex">The starting index</param>
		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			((IDictionary<TKey, TValue>) _dictionary).CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// The number of items
		/// </summary>
		public int Count
		{
			get { return _dictionary.Count; }
		}

		/// <summary>
		/// If it is read only. Always returns false.
		/// </summary>
		public bool IsReadOnly
		{
			get { return false; }
		}

		/// <summary>
		/// Removes an item.
		/// </summary>
		/// <param name="item">The item to remove.</param>
		/// <returns>True if the item was removed, else false.</returns>
		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			return _Remove(item.Key);
		}

		/// <summary>
		/// Gets an enumertor for items.
		/// </summary>
		/// <returns>An enumerator.</returns>
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

		/// <summary>
		/// Gets an enumertor for items.
		/// </summary>
		/// <returns>An enumerator.</returns>
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return _dictionary.GetEnumerator();
		}
	}
}
