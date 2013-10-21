using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Common
{
	/// <summary>
	/// For when an item is removed from a <see cref="IDicionary"/>
	/// </summary>
	/// <typeparam name="TKey">The key</typeparam>
	/// <typeparam name="TValue">The value</typeparam>
	public class KeyValueRemovedArgs<TKey, TValue> : EventArgs
	{
		public TKey Key { get; set; }
		public TValue Value { get; set; }
	}
}
