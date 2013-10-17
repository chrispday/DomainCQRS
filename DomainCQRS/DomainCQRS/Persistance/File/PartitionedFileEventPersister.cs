using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DomainCQRS.Common;
using DomainCQRS.Persister;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class PartitionedFileEventPersisterConfigure
	{
		public static int DefaultEventStreamCacheCapacityPerPartition = 50;
		public static int DefaultEventStreamBufferSize = 1024 * 8;

		public static IConfigure PartitionedFileEventPersister(this IConfigure configure, int maximumPartitions, string directory) { return configure.PartitionedFileEventPersister(maximumPartitions, directory, DefaultEventStreamCacheCapacityPerPartition, DefaultEventStreamBufferSize); }
		public static IConfigure PartitionedFileEventPersister(this IConfigure configure, int maximumPartitions, string directory, int eventStreamCacheCapacityPerPartition, int eventStreamBufferSize)
		{
			configure.Registry
				.BuildInstancesOf<IEventPersister>()
				.TheDefaultIs(Registry.Instance<IEventPersister>()
					.UsingConcreteType<PartitionedFileEventPersister>()
					.WithProperty("directory").EqualTo(directory)
					.WithProperty("maximumPartitions").EqualTo(maximumPartitions)
					.WithProperty("eventStreamCacheCapacityPerPartition").EqualTo(eventStreamCacheCapacityPerPartition)
					.WithProperty("eventStreamBufferSize").EqualTo(eventStreamBufferSize))
				.AsSingletons();
			return configure;
		}
	}
}

namespace DomainCQRS.Persister
{
	public class PartitionedFileEventPersister : IPartitionedEventPersister
	{
		private readonly string _directory;
		public string Directory { get { return _directory; } }
		private readonly int _eventStreamCacheCapacityPerPartition;
		public int EventStreamCacheCapacityPerPartition { get { return _eventStreamCacheCapacityPerPartition; } }
		private readonly int _eventStreamBufferSize;
		public int EventStreamBufferSize { get { return _eventStreamBufferSize; } }
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		private readonly int _maximumPartitions;
		public int MaximumPartitions { get { return _maximumPartitions; } }

		private FileEventPersister[] _fileEventStoreProviders;

		public PartitionedFileEventPersister(ILogger logger, string directory, int maximumPartitions, int eventStreamCacheCapacityPerPartition, int eventStreamBufferSize)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == directory)
			{
				throw new ArgumentNullException("directory");
			}
			if (0 >= maximumPartitions)
			{
				throw new ArgumentOutOfRangeException("maximumPartitions");
			}
			if (0 >= eventStreamCacheCapacityPerPartition)
			{
				throw new ArgumentOutOfRangeException("eventStreamCacheCapacityPerPartition");
			}
			if (0 >= eventStreamBufferSize)
			{
				throw new ArgumentOutOfRangeException("eventStreamBufferSize");
			}

			_logger = logger;
			_directory = directory;
			_maximumPartitions = maximumPartitions;
			_eventStreamCacheCapacityPerPartition = eventStreamCacheCapacityPerPartition;
			_eventStreamBufferSize = eventStreamBufferSize;
		}

		public IEventPersister EnsureExists()
		{
			if (2 > MaximumPartitions)
			{
				throw new ArgumentOutOfRangeException("MaximumPartitions", MaximumPartitions, "Must be 2 or more.");
			}

			if (!System.IO.Directory.Exists(Directory))
			{
				System.IO.Directory.CreateDirectory(Directory);
			}

			_fileEventStoreProviders = new FileEventPersister[MaximumPartitions];
			for (int i = 0; i < MaximumPartitions; i++)
			{
				_fileEventStoreProviders[i] = new FileEventPersister(
					Logger,
					Path.Combine(Directory, i.ToString()),
					EventStreamCacheCapacityPerPartition,
					EventStreamBufferSize
					).EnsureExists() as FileEventPersister;
			}

			return this;
		}

		public IEventPersister Save(EventToStore eventToStore)
		{
			var fileEventStoreProvider = _fileEventStoreProviders[GetIndex(eventToStore.AggregateRootId)];
			lock (fileEventStoreProvider)
			{
				return fileEventStoreProvider.Save(eventToStore);
			}
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			var fileEventStoreProvider = _fileEventStoreProviders[GetIndex(aggregateRootId)];
			lock (fileEventStoreProvider)
			{
				return fileEventStoreProvider.Load(aggregateRootId, fromVersion, toVersion, fromTimestamp, toTimestamp);
			}
		}

		public IEventPersisterPosition CreatePosition()
		{
			return new PartitionedFileEventPersisterPosition(MaximumPartitions);
		}

		public IEventPersisterPosition LoadPosition(Guid subscriberId)
		{
			var position = new PartitionedFileEventPersisterPosition(MaximumPartitions);
			for (int i = 0; i < MaximumPartitions; i++)
			{
				position.Positions[i] = _fileEventStoreProviders[i].LoadPosition(subscriberId);
			}
			return position;
		}

		public IEventPersister SavePosition(Guid subscriberId, IEventPersisterPosition position)
		{
			return SavePosition(subscriberId, position as PartitionedFileEventPersisterPosition);
		}

		public IEventPersister SavePosition(Guid subscriberId, PartitionedFileEventPersisterPosition position)
		{
			for (int i = 0; i < MaximumPartitions; i++)
			{
				_fileEventStoreProviders[i].SavePosition(subscriberId, position.Positions[i]);
			}
			return this;
		}

		//public IEnumerable<EventToStore> Load(IEventStoreProviderPosition to)
		//{
		//	return Load(CreatePosition(), to);
		//}

		public IEnumerable<EventToStore> Load(IEventPersisterPosition from, IEventPersisterPosition to)
		{
			return Load(from as PartitionedFileEventPersisterPosition, to as PartitionedFileEventPersisterPosition);
		}

		public IEnumerable<EventToStore> Load(PartitionedFileEventPersisterPosition from, PartitionedFileEventPersisterPosition to)
		{
			if (null == from)
			{
				from = new PartitionedFileEventPersisterPosition(MaximumPartitions);
			}

			for (int i = 0; i < MaximumPartitions; i++)
			{
				foreach (var @event in _fileEventStoreProviders[i].Load(from.Positions[i], to.Positions[i]))
				{
					yield return @event;
				}
			}
		}

		public void Dispose()
		{
			foreach (var fileEventStoreProvider in _fileEventStoreProviders)
			{
				fileEventStoreProvider.Dispose();
			}
			_fileEventStoreProviders = null;
		}

		private int GetIndex(Guid guid)
		{
			var hashCode = guid.GetHashCode();
			return (hashCode >= 0 ? hashCode : -hashCode) % MaximumPartitions;
		}
	}
}
