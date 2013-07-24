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
		public static IConfigure FileEventStoreProvider(this IConfigure configure, string directory)
		{
			var c = configure as Configure;
			c.EventStoreProvider = new FileEventStoreProvider() { Directory = directory, Logger = c.Logger }.EnsureExists();
			return configure;
		}
	}
}

namespace Yeast.EventStore.Provider
{
	public class FileEventStoreProvider : IEventStoreProvider, IDisposable
	{
		public string Directory { get; set; }
		public int EventStreamCapacity { get; set; }
		public int EventStreamBufferSize { get; set; }
		public ILogger Logger { get; set; }
		private LRUDictionary<Guid, FileEventStream> FileEventStreams;

		public FileEventStoreProvider()
		{
			EventStreamCapacity = 10000;
			EventStreamBufferSize = 1024 * 8;
		}

		public IEventStoreProvider EnsureExists()
		{
			if (!System.IO.Directory.Exists(Directory))
			{
				Logger.Information("Creating directory {0}", Directory);
				System.IO.Directory.CreateDirectory(Directory);
			}
			FileEventStreams = new LRUDictionary<Guid, FileEventStream>(EventStreamCapacity);
			FileEventStreams.Removed += FileEventStreamRemoved;
			return this;
		}

		private void FileEventStreamRemoved(object sender, EventArgs e)
		{
			var stream = (KeyValuePair<Guid, FileEventStream>)sender;
			stream.Value.Dispose();
		}

		public IEventStoreProvider Save(EventToStore eventToStore)
		{
			FileEventStream fileEventStream = GetFileEventStream(eventToStore.AggregateRootId);
			fileEventStream.Save(eventToStore);

			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			FileEventStream fileEventStream = GetFileEventStream(aggregateRootId);
			return fileEventStream.Load(aggregateRootId, fromVersion, toVersion, fromTimestamp, toTimestamp);
		}

		private FileEventStream GetFileEventStream(Guid aggregateRootId)
		{
			FileEventStream fileEventStream;
			if (!FileEventStreams.TryGetValue(aggregateRootId, out fileEventStream))
			{
				FileEventStreams.Add(aggregateRootId, fileEventStream = new FileEventStream(Logger, aggregateRootId, Directory, EventStreamBufferSize));
			}
			return fileEventStream;
		}

		public void Dispose()
		{
			foreach (var item in FileEventStreams)
			{
				item.Value.Dispose();
			}
		}
	}
}
