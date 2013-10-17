using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;
using DomainCQRS.Persister;

namespace DomainCQRS
{
	public static class MemoryEventPersisterConfigure
	{
		public static IConfigure MemoryEventPersister(this IConfigure configure)
		{
			configure.Registry
				.BuildInstancesOf<IEventPersister>()
				.TheDefaultIsConcreteType<MemoryEventPersister>()
				.AsSingletons();
			return configure;
		}
	}
}

namespace DomainCQRS.Persister
{
	public class MemoryEventPersister : IEventPersister
	{
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }

		private Dictionary<Guid, List<EventToStore>> _eventStore;
		private Dictionary<Guid, int> _versionTracker;
		private Dictionary<Guid, IEventPersisterPosition> _positions = new Dictionary<Guid, IEventPersisterPosition>();

		public MemoryEventPersister(ILogger logger)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}

			_logger = logger;
		}

		public IEventPersister EnsureExists()
		{
			_eventStore = new Dictionary<Guid, List<EventToStore>>();
			_versionTracker = new Dictionary<Guid, int>();
			return this;
		}

		public IEventPersister Save(EventToStore eventToStore)
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
			lock (_eventStore)
			{
				if (!_eventStore.TryGetValue(aggregateRootId, out events))
				{
					yield break;
				}
			}
			
			List<EventToStore> eventsCopy;
			lock (events)
			{
				eventsCopy = new List<EventToStore>(events);
			}

			foreach (var @event in eventsCopy)
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

		public IEnumerable<EventToStore> Load(IEventPersisterPosition from, IEventPersisterPosition to)
		{
			return Load(from as MemoryEventPersisterPostion, to as MemoryEventPersisterPostion);
		}

		public IEventPersisterPosition CreatePosition()
		{
			return new MemoryEventPersisterPostion();
		}

		public IEventPersisterPosition LoadPosition(Guid subscriberId)
		{
			IEventPersisterPosition position = new MemoryEventPersisterPostion();
			_positions.TryGetValue(subscriberId, out position);
			return position;
		}

		public IEventPersister SavePosition(Guid subscriberId, IEventPersisterPosition position)
		{
			_positions[subscriberId] = position;
			return this;
		}

		private IEnumerable<EventToStore> Load(MemoryEventPersisterPostion from, MemoryEventPersisterPostion to)
		{
			Logger.Verbose("from {0} to {1}", from, to);

			Dictionary<Guid, List<EventToStore>> eventStoreCopy;
			lock (_eventStore)
			{
				eventStoreCopy = new Dictionary<Guid, List<EventToStore>>(_eventStore);
			}

			foreach (var item in eventStoreCopy)
			{
				int fromPostion = 0;
				from.Positions.TryGetValue(item.Key, out fromPostion);

				List<EventToStore> events;
				lock (item.Value)
				{
					events = new List<EventToStore>(item.Value);
				}

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
