using System;
using System.Collections.Generic;
using System.IO;
using DomainCQRS.Common;
using DomainCQRS.Persister;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class FileEventPersisterConfigure
	{
		public static int DefaultEventStreamCacheCapacity = 10000;
		public static int DefaultEventStreamBufferSize = 8 * 1024;
		/// <summary>
		/// Configure DomainCQRS to use the <see cref="FileEventPersister"/>.
		/// Uses a default cache capacity and default stream buffer.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/></param>
		/// <param name="directory">The directory where event stream files will be stored.</param>
		/// <returns>The <see cref="IConfigure"/></returns>
		public static IConfigure FileEventPersister(this IConfigure configure, string directory) { return configure.FileEventPersister(directory, DefaultEventStreamCacheCapacity, DefaultEventStreamBufferSize); }
		/// <summary>
		/// Configure DomainCQRS to use the <see cref="FileEventPersister"/>.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/></param>
		/// <param name="directory">The directory where event stream files will be stored.</param>
		/// <param name="eventStreamCacheCapacity">The number of event streams to keep in memory using an LRU cache.</param>
		/// <param name="eventStreamBufferSize">The default buffer size to use when opening event streams.</param>
		/// <returns>The <see cref="IConfigure"/></returns>
		public static IConfigure FileEventPersister(this IConfigure configure, string directory, int eventStreamCacheCapacity, int eventStreamBufferSize)
		{
			configure.Registry
				.BuildInstancesOf<IEventPersister>()
				.TheDefaultIs(Registry.Instance<IEventPersister>()
					.UsingConcreteType<FileEventPersister>()
					.WithProperty("directory").EqualTo(directory)
					.WithProperty("eventStreamCacheCapacity").EqualTo(eventStreamCacheCapacity)
					.WithProperty("eventStreamBufferSize").EqualTo(eventStreamBufferSize))
				.AsSingletons();
			return configure;
		}
	}
}

namespace DomainCQRS.Persister
{
	/// <summary>
	/// Persists events to files.
	/// </summary>
	public class FileEventPersister : IEventPersister
	{
		private readonly string _directory;
		public string Directory { get { return _directory; } }
		private readonly int _eventStreamCacheCapacity;
		public int EventStreamCacheCapacity { get { return _eventStreamCacheCapacity; } }
		private readonly int _eventStreamBufferSize;
		public int EventStreamBufferSize { get { return _eventStreamBufferSize; } }
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }

		private LRUDictionary<Guid, FileEventStream> _fileEventStreams;
		private bool _storeAggregateId = false;
		private string _eventDirectory;
		private string _subscriberDirectory;

		public FileEventPersister(ILogger logger, string directory, int eventStreamCacheCapacity, int eventStreamBufferSize)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == directory)
			{
				throw new ArgumentNullException("directory");
			}
			if (0 >= eventStreamCacheCapacity)
			{
				throw new ArgumentOutOfRangeException("eventStreamCacheCapacity");
			}
			if (0 >= eventStreamBufferSize)
			{
				throw new ArgumentOutOfRangeException("eventStreamBufferSize");
			}

			_logger = logger;
			_directory = directory;
			_eventStreamCacheCapacity = eventStreamCacheCapacity;
			_eventStreamBufferSize = eventStreamBufferSize;
		}

		public IEventPersister EnsureExists()
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

		public IEventPersister Save(EventToStore eventToStore)
		{
			GetFileEventStream(eventToStore.AggregateRootId).Save(eventToStore);
			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			return GetFileEventStream(aggregateRootId).Load(aggregateRootId, fromVersion, toVersion, fromTimestamp, toTimestamp);
		}

		public IEventPersisterPosition CreatePosition()
		{
			return new FileEventPersisterPosition();
		}

		public IEventPersisterPosition LoadPosition(Guid subscriberId)
		{
			var positions = new FileEventPersisterPosition();

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

		public IEventPersister SavePosition(Guid subscriberId, IEventPersisterPosition position)
		{
			return SaveEventStoreProviderPosition(subscriberId, position as FileEventPersisterPosition);
		}

		public IEventPersister SaveEventStoreProviderPosition(Guid subscriberId, FileEventPersisterPosition position)
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

		public IEnumerable<EventToStore> Load(IEventPersisterPosition from, IEventPersisterPosition to)
		{
			return Load(from as FileEventPersisterPosition, to as FileEventPersisterPosition);
		}

		private IEnumerable<EventToStore> Load(FileEventPersisterPosition from, FileEventPersisterPosition to)
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
