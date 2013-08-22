using System;
using System.Collections.Generic;
using System.IO;

using System.Text;
using Yeast.EventStore.Provider;

namespace Yeast.EventStore
{
	public static class PartitionedFileEventStoreProviderConfigure
	{
		public static int DefaultEventStreamCacheCapacityPerPartition = 50;
		public static int DefaultEventStreamBufferSize = 1024 * 8;

		public static IConfigure PartitionedFileEventStoreProvider(this IConfigure configure, int maximumPartitions, string directory) { return configure.PartitionedFileEventStoreProvider(maximumPartitions, directory, DefaultEventStreamCacheCapacityPerPartition, DefaultEventStreamBufferSize); }
		public static IConfigure PartitionedFileEventStoreProvider(this IConfigure configure, int maximumPartitions, string directory, int eventStreamCacheCapacityPerPartition, int eventStreamBufferSize)
		{
			var c = configure as Configure;
			c.EventStoreProvider = new PartitionedFileEventStoreProvider()
			{
				MaximumPartitions = maximumPartitions, 
				Directory = directory,
				Logger = c.Logger,
				EventStreamCacheCapacityPerPartition = eventStreamCacheCapacityPerPartition,
				EventStreamBufferSize = eventStreamBufferSize
			}.EnsureExists();
			return configure;
		}
	}
}

namespace Yeast.EventStore.Provider
{
	public class PartitionedFileEventStoreProvider : IPartitionedEventStoreProvider
	{
		public int MaximumPartitions { get; set; }
		public Common.ILogger Logger { get; set; }
		public string Directory { get; set; }
		public int EventStreamCacheCapacityPerPartition { get; set; }
		public int EventStreamBufferSize { get; set; }
		private FileEventStoreProvider[] _fileEventStoreProviders;

		public PartitionedFileEventStoreProvider()
		{
			EventStreamCacheCapacityPerPartition = PartitionedFileEventStoreProviderConfigure.DefaultEventStreamCacheCapacityPerPartition;
			EventStreamBufferSize = PartitionedFileEventStoreProviderConfigure.DefaultEventStreamBufferSize;
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
				_fileEventStoreProviders[i] = new FileEventStoreProvider()
				{
					Logger = Logger,
					Directory = Path.Combine(Directory, i.ToString()),
					EventStreamBufferSize = EventStreamBufferSize,
					EventStreamCacheCapacity = EventStreamCacheCapacityPerPartition
				}.EnsureExists() as FileEventStoreProvider;
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
