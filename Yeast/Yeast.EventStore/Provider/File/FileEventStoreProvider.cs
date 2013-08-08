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
	public class FileEventStoreProvider : IEventStoreProvider
	{
		public string Directory { get; set; }
		public int EventStreamCacheCapacity { get; set; }
		public int EventStreamBufferSize { get; set; }
		public ILogger Logger { get; set; }
		private LRUDictionary<Guid, FileEventStream> _fileEventStreams;
		private bool _storeAggregateId = false;
		private string _eventDirectory;
		private string _subscriberDirectory;

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
				_eventDirectory = Path.Combine(Directory, "Event");
				System.IO.Directory.CreateDirectory(_eventDirectory);
				_subscriberDirectory = Path.Combine(Directory, "Subscriber");
				System.IO.Directory.CreateDirectory(_subscriberDirectory);
			}
			_eventDirectory = Path.Combine(Directory, "Event");
			if (!System.IO.Directory.Exists(_eventDirectory))
			{
				Logger.Information("Creating directory {0}", _eventDirectory);
				System.IO.Directory.CreateDirectory(_eventDirectory);
			}
			_subscriberDirectory = Path.Combine(Directory, "Subscriber");
			if (!System.IO.Directory.Exists(_subscriberDirectory))
			{
				Logger.Information("Creating directory {0}", _subscriberDirectory);
				System.IO.Directory.CreateDirectory(_subscriberDirectory);
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
			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			return GetFileEventStream(aggregateRootId).Load(aggregateRootId, fromVersion, toVersion, fromTimestamp, toTimestamp);
		}

		public IEventStoreProviderPosition CreatePosition()
		{
			return new FileEventStoreProviderPosition();
		}

		public IEventStoreProviderPosition LoadPosition(Guid subscriberId)
		{
			var positions = new FileEventStoreProviderPosition();

			try
			{
				using (var reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(GetSubscribtionFilename(subscriberId)))))
				{
					while (true)
					{
						var guidBuffer = reader.ReadBytes(16);
						if (0 == guidBuffer.Length)
						{
							break;
						}
						var position = reader.ReadInt64();
						positions.Positions[new Guid(guidBuffer)] = position;
					}
				}
			}
			catch (FileNotFoundException) { }

			return positions;
		}

		public IEventStoreProvider SavePosition(Guid subscriberId, IEventStoreProviderPosition position)
		{
			return SaveEventStoreProviderPosition(subscriberId, position as FileEventStoreProviderPosition);
		}

		public IEventStoreProvider SaveEventStoreProviderPosition(Guid subscriberId, FileEventStoreProviderPosition position)
		{
			var stream = new MemoryStream();
			var writer = new BinaryWriter(stream);
			foreach (var item in position.Positions)
			{
				writer.Write(item.Key.ToByteArray());
				writer.Write(item.Value);
			}
			stream.Flush();

			File.WriteAllBytes(GetSubscribtionFilename(subscriberId), stream.ToArray());

			return this;
		}

		private string GetSubscribtionFilename(Guid subscriberId)
		{
			return Path.Combine(_subscriberDirectory, subscriberId.ToString());
		}

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			return Load(from as FileEventStoreProviderPosition, to as FileEventStoreProviderPosition);
		}

		private IEnumerable<EventToStore> Load(FileEventStoreProviderPosition from, FileEventStoreProviderPosition to)
		{
			foreach (var file in new DirectoryInfo(_eventDirectory).GetFiles())
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
		}

		private FileEventStream GetFileEventStream(Guid aggregateRootId)
		{
			FileEventStream fileEventStream;
			if (!_fileEventStreams.TryGetValue(aggregateRootId, out fileEventStream))
			{
				_fileEventStreams.Add(aggregateRootId, fileEventStream = new FileEventStream(Logger, aggregateRootId, _eventDirectory, EventStreamBufferSize, true, _storeAggregateId));
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
