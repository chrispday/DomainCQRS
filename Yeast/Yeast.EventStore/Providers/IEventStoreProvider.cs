using System;
using System.Collections.Generic;

namespace Yeast.EventStore
{
    public interface IEventStoreProvider
    {
        IEventStoreProvider EnsureExists();
		  IEventStoreProvider Save(EventToStore eventToStore);
		  IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);
    }
}
