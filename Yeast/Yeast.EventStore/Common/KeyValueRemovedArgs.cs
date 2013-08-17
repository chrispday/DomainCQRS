using System;
using System.Collections.Generic;

using System.Text;

namespace Yeast.EventStore.Common
{
	public class KeyValueRemovedArgs<TKey, TValue> : EventArgs
	{
		public TKey Key { get; set; }
		public TValue Value { get; set; }
	}
}
