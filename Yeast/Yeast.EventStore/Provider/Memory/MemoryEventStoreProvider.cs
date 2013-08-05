using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Provider;

namespace Yeast.EventStore
{
	public static class MemoryEventStoreProviderConfigure
	{
		public static IConfigure MemoryEventStoreProvider(this IConfigure configure)
		{
			var c = configure as Configure;
			c.EventStoreProvider = new MemoryEventStoreProvider()
			{
				Logger = c.Logger
			}.EnsureExists();
			return configure;
		}
	}
}

namespace Yeast.EventStore.Provider
{
	public class MemoryEventStoreProvider : IEventStoreProvider
	{
		public Common.ILogger Logger { get; set; }
		private Dictionary<Guid, List<EventToStore>> _eventStore;
		private Dictionary<Guid, int> _versionTracker;

		public IEventStoreProvider EnsureExists()
		{
			_eventStore = new Dictionary<Guid, List<EventToStore>>();
			_versionTracker = new Dictionary<Guid, int>();
			return this;
		}

		public IEventStoreProvider Save(EventToStore eventToStore)
		{
			List<EventToStore> events;
			lock (_eventStore)
			{
				if (!_eventStore.TryGetValue(eventToStore.AggregateRootId, out events))
				{
					_eventStore[eventToStore.AggregateRootId] = events = new List<EventToStore>();
					_versionTracker[eventToStore.AggregateRootId] = 0;
				}
			}

			lock (events)
			{
				var expectedVersion = _versionTracker[eventToStore.AggregateRootId] + 1;
				if (eventToStore.Version != expectedVersion)
				{
					throw new ConcurrencyException();
				}

				events.Add(eventToStore);
				_versionTracker[eventToStore.AggregateRootId] = expectedVersion;
			}

			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			List<EventToStore> events;
			if (!_eventStore.TryGetValue(aggregateRootId, out events))
			{
				yield break;
			}

			foreach (var @event in new List<EventToStore>(events))
			{
				if (@event.AggregateRootId == aggregateRootId
					&& @event.Version >= fromVersion.GetValueOrDefault(-1)
					&& @event.Version <= toVersion.GetValueOrDefault(int.MaxValue)
					&& @event.Timestamp >= fromTimestamp.GetValueOrDefault(DateTime.MinValue)
					&& @event.Timestamp <= toTimestamp.GetValueOrDefault(DateTime.MaxValue))
				{
					yield return @event;
				}
			}
		}

		public IEventStoreProviderPosition CreateEventStoreProviderPosition()
		{
			return new MemoryEventStoreProviderPostion();
		}

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			return Load(from as MemoryEventStoreProviderPostion, to as MemoryEventStoreProviderPostion);
		}

		private IEnumerable<EventToStore> Load(MemoryEventStoreProviderPostion from, MemoryEventStoreProviderPostion to)
		{
			Logger.Verbose("from {0} to {1}", from, to);

			foreach (var item in new Dictionary<Guid, List<EventToStore>>(_eventStore))
			{
				int fromPostion = 0;
				from.Positions.TryGetValue(item.Key, out fromPostion);

				var events = new List<EventToStore>(item.Value);

				int toPosition = events.Count;
				//to.Positions.TryGetValue(item.Key, out toPosition);

				int i = fromPostion;
				for (; i < toPosition; i++)
				{
					to.Positions[item.Key] = i + 1;
					yield return events[i];
				}
				to.Positions[item.Key] = i;
			}
		}

		public void Dispose()
		{
			_eventStore = null;
			_versionTracker = null;
		}
	}
}
