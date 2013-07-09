using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yeast.EventStore.Test
{
	public class MockEventStore : IEventStore
	{
		public IEventStoreProvider EventStoreProvider
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public List<Tuple<Guid, int, object>> Saved = new List<Tuple<Guid, int, object>>();
		public IEventStore Save<T>(Guid aggregateId, int version, T data)
		{
			Saved.Add(Tuple.Create(aggregateId, version, (object)data));
			return this;
		}

		public IEnumerable<StoredEvent> Load(Guid aggregateId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			throw new NotImplementedException();
		}
	}
}
