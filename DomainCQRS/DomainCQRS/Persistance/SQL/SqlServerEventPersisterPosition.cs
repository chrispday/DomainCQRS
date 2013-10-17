using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Persister
{
	public class SqlServerEventPersisterPosition : IEventPersisterPosition
	{
		public long Position;

		public override string ToString()
		{
			return Position.ToString();
		}
	}
}
