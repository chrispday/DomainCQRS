using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Yeast.EventStore.Common;
using Yeast.EventStore.Provider;

namespace Yeast.EventStore
{
	public static class FileEventStoreProviderConfigure
	{
		public static int DefaultEventStreamCacheCapacity = 10000;
		public static int DefaultEventStreamBufferSize = 8 * 1024;
		public static IConfigure FileEventStoreProvider(this IConfigure configure, string directory) { return configure.FileEventStoreProvider(directory, DefaultEventStreamCacheCapacity, DefaultEventStreamBufferSize); }
		public static IConfigure FileEventStoreProvider(this IConfigure configure, string directory, int eventStreamCacheCapacity, int eventStreamBufferSize)
		{
			if (1 > eventStreamCacheCapacity)
			{
				throw new ArgumentOutOfRangeException("eventStreamCacheCapacity", eventStreamCacheCapacity, "eventStreamCacheCapacity cannot be less than 1.");
			}
			if (1 > eventStreamBufferSize)
			{
				throw new ArgumentOutOfRangeException("eventStreamBufferSize", eventStreamBufferSize, "eventStreamBufferSize cannot be less than 1.");
			}

			var c = configure as Configure;
			c.EventStoreProvider = new FileEventStoreProvider()
			{ 
				Directory = directory, 
				Logger = c.Logger, 
				EventStreamCacheCapacity = eventStreamCacheCapacity,
				EventStreamBufferSize = eventStreamBufferSize
			}.EnsureExists();
			return configure;
		}
	}
}

namespace Yeast.EventStore.Provider
{
	public class FileEventStoreProvider : IEventStoreProvider, IDisposable
	{
		public string Directory { get; set; }
		public int EventStreamCacheCapacity { get; set; }
		public int EventStreamBufferSize { get; set; }
		public ILogger Logger { get; set; }
		private LRUDictionary<Guid, FileEventStream> _fileEventStreams;
		private volatile bool _newEvents = true;
		private volatile bool _newEventsSince = false;
		private bool _storeAggregateId = false;

		public FileEventStoreProvider()
		{
			EventStreamCacheCapacity = FileEventStoreProviderConfigure.DefaultEventStreamCacheCapacity;
			EventStreamBufferSize = FileEventStoreProviderConfigure.DefaultEventStreamBufferSize;
		}

		public IEventStoreProvider EnsureExists()
		{
			if (!System.IO.Directory.Exists(Directory))
			{
				Logger.Information("Creating directory {0}", Directory);
				System.IO.Directory.CreateDirectory(Directory);
			}
			_fileEventStreams = new LRUDictionary<Guid, FileEventStream>(EventStreamCacheCapacity);
			_fileEventStreams.Removed += FileEventStream_Removed;
			return this;
		}

		private void FileEventStream_Removed(object sender, KeyValueRemovedArgs<Guid, FileEventStream> e)
		{
			e.Value.Dispose();
		}

		public IEventStoreProvider Save(EventToStore eventToStore)
		{
			GetFileEventStream(eventToStore.AggregateRootId).Save(eventToStore);

			_newEvents = true;
			_newEventsSince = true;

			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			return GetFileEventStream(aggregateRootId).Load(aggregateRootId, fromVersion, toVersion, fromTimestamp, toTimestamp);
		}

		public IEventStoreProviderPosition CreateEventStoreProviderPosition()
		{
			return new FileEventStoreProviderPosition();
		}

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition to)
		{
			return Load(CreateEventStoreProviderPosition(), to);
		}

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			return Load(from as FileEventStoreProviderPosition, to as FileEventStoreProviderPosition);
		}

		private IEnumerable<EventToStore> Load(FileEventStoreProviderPosition from, FileEventStoreProviderPosition to)
		{
			if (!_newEvents)
			{
				Logger.Verbose("No new events.");
				//yield break;
			}
			_newEventsSince = false;

			foreach (var file in new DirectoryInfo(Directory).GetFiles())
			{
				var aggregateRootId = new Guid(file.Name);

				file.Refresh();
				if (from.Positions.ContainsKey(aggregateRootId)
					&& file.Length <= from.Positions[aggregateRootId])
				{
					Logger.Verbose("Skipping file length {0} <= position {1}", file.Length, from.Positions[aggregateRootId]);
					to.Positions[aggregateRootId] = from.Positions[aggregateRootId];
					continue;
				}

				bool dispose;
				FileEventStream fileEventStream;
				if (dispose = !_fileEventStreams.TryGetValue(aggregateRootId, out fileEventStream))
				{
					fileEventStream = new FileEventStream(Logger, aggregateRootId, Directory, EventStreamBufferSize, true, _storeAggregateId);
				}

				try
				{
					foreach (var @event in fileEventStream.Load(aggregateRootId, from, to))
					{
						yield return @event;
					}
				}
				finally
				{
					if (dispose && null != fileEventStream)
					{
						fileEventStream.Dispose();
					}
				}
			}

			_newEvents = _newEventsSince;
		}

		private FileEventStream GetFileEventStream(Guid aggregateRootId)
		{
			FileEventStream fileEventStream;
			if (!_fileEventStreams.TryGetValue(aggregateRootId, out fileEventStream))
			{
				_fileEventStreams.Add(aggregateRootId, fileEventStream = new FileEventStream(Logger, aggregateRootId, Directory, EventStreamBufferSize, true, _storeAggregateId));
			}
			return fileEventStream;
		}

		public void Dispose()
		{
			foreach (var item in _fileEventStreams)
			{
				item.Value.Dispose();
			}
			_fileEventStreams = null;
		}
	}
}
