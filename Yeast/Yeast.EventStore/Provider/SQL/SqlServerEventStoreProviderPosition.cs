using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Provider
{
	public class SqlServerEventStoreProviderPosition : IEventStoreProviderPosition
	{
		public int Position;

		public override string ToString()
		{
			return Position.ToString();
		}
	}
}
