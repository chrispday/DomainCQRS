using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DomainCQRS.Common;
using DomainCQRS.Provider;

namespace DomainCQRS
{
	public static class PartitionedFileEventStoreProviderConfigure
	{
		public static int DefaultEventStreamCacheCapacityPerPartition = 50;
		public static int DefaultEventStreamBufferSize = 1024 * 8;

		public static IConfigure PartitionedFileEventStoreProvider(this IConfigure configure, int maximumPartitions, string directory) { return configure.PartitionedFileEventStoreProvider(maximumPartitions, directory, DefaultEventStreamCacheCapacityPerPartition, DefaultEventStreamBufferSize); }
		public static IConfigure PartitionedFileEventStoreProvider(this IConfigure configure, int maximumPartitions, string directory, int eventStreamCacheCapacityPerPartition, int eventStreamBufferSize)
		{
			var c = configure as Configure;
			c.EventStoreProvider = new PartitionedFileEventStoreProvider(
				c.Logger,
				directory,
				maximumPartitions,
				eventStreamCacheCapacityPerPartition,
				eventStreamBufferSize
				).EnsureExists();
			return configure;
		}
	}
}

namespace DomainCQRS.Provider
{
	public class PartitionedFileEventStoreProvider : IPartitionedEventStoreProvider
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

		private FileEventStoreProvider[] _fileEventStoreProviders;

		public PartitionedFileEventStoreProvider(ILogger logger, string directory, int maximumPartitions, int eventStreamCacheCapacityPerPartition, int eventStreamBufferSize)
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

		public IEventStoreProvider EnsureExists()
		{
			if (2 > MaximumPartitions)
			{
				throw new ArgumentOutOfRangeException("MaximumPartitions", MaximumPartitions, "Must be 2 or more.");
			}

			if (!System.IO.Directory.Exists(Directory))
			{
				System.IO.Directory.CreateDirectory(Directory);
			}

			_fileEventStoreProviders = new FileEventStoreProvider[MaximumPartitions];
			for (int i = 0; i < MaximumPartitions; i++)
			{
				_fileEventStoreProviders[i] = new FileEventStoreProvider(
					Logger,
					Path.Combine(Directory, i.ToString()),
					EventStreamCacheCapacityPerPartition,
					EventStreamBufferSize
					).EnsureExists() as FileEventStoreProvider;
			}

			return this;
		}

		public IEventStoreProvider Save(EventToStore eventToStore)
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

		public IEventStoreProviderPosition CreatePosition()
		{
			return new PartitionedFileEventStoreProviderPosition(MaximumPartitions);
		}

		public IEventStoreProviderPosition LoadPosition(Guid subscriberId)
		{
			var position = new PartitionedFileEventStoreProviderPosition(MaximumPartitions);
			for (int i = 0; i < MaximumPartitions; i++)
			{
				position.Positions[i] = _fileEventStoreProviders[i].LoadPosition(subscriberId);
			}
			return position;
		}

		public IEventStoreProvider SavePosition(Guid subscriberId, IEventStoreProviderPosition position)
		{
			return SavePosition(subscriberId, position as PartitionedFileEventStoreProviderPosition);
		}

		public IEventStoreProvider SavePosition(Guid subscriberId, PartitionedFileEventStoreProviderPosition position)
		{
			for (int i = 0; i < MaximumPartitions; i++)
			{
				_fileEventStoreProviders[i].SavePosition(subscriberId, position.Positions[i]);
			}
			return this;
		}

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition to)
		{
			return Load(CreatePosition(), to);
		}

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			return Load(from as PartitionedFileEventStoreProviderPosition, to as PartitionedFileEventStoreProviderPosition);
		}

		public IEnumerable<EventToStore> Load(PartitionedFileEventStoreProviderPosition from, PartitionedFileEventStoreProviderPosition to)
		{
			if (null == from)
			{
				from = new PartitionedFileEventStoreProviderPosition(MaximumPartitions);
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
