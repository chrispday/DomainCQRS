using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Yeast.EventStore.Common;

namespace Yeast.EventStore.Provider
{
	public class FileEventStoreProvider : IEventStoreProvider, IDisposable
	{

		public string Directory { get; set; }
		public TimeSpan LockTimeout { get; set; }
		public int OpenFileRetryCount { get; set; }
		public int VersionTrackerCapacity { get; set; }
		public int EventStreamCapacity { get; set; }
		private LRUDictionary<Guid, int> VersionTracker;
		private EventStreams<byte> EventStreams;

		public FileEventStoreProvider()
		{
			LockTimeout = TimeSpan.FromSeconds(5);
			OpenFileRetryCount = 5;
			VersionTrackerCapacity = 10000;
			EventStreamCapacity = 100;
		}

		private bool VersionExists(Guid aggregateRootId, int versionToCheck)
		{
			if (null == VersionTracker)
			{
				VersionTracker = new LRUDictionary<Guid, int>(VersionTrackerCapacity);
			}

			int lastSeenVersion = -1;
			if (VersionTracker.TryGetValue(aggregateRootId, out lastSeenVersion))
			{
				return versionToCheck <= lastSeenVersion;
			}

			var index = aggregateRootId.ToByteArray()[0];

			if (!EventStreams.VersionTrackerLastPositions.ContainsKey(index))
			{
				EventStreams.VersionTrackerLastPositions[index] = 0;
			}

			var reader = EventStreams.Reader(index);
			var stream = reader.BaseStream;
			var streamLength = stream.Length;

			var position = EventStreams.VersionTrackerLastPositions[index];
			if (stream.Position != position)
			{
				stream.Seek(position, SeekOrigin.Begin);
			}

			while (true)
			{
				if (position >= streamLength) { break; }

				var savedAggregateRootId = new Guid(reader.ReadBytes(16));
				var dataSize = reader.ReadInt32();
				var version = reader.ReadInt32();

				stream.Seek(sizeof(long) + dataSize, SeekOrigin.Current);
				position += 16 + sizeof(int) + sizeof(int) + sizeof(long) + dataSize;

				VersionTracker[savedAggregateRootId] = version;

				if (aggregateRootId == savedAggregateRootId
					&& version == versionToCheck)
				{
					EventStreams.VersionTrackerLastPositions[index] = position;
					return true;
				}
			}

			EventStreams.VersionTrackerLastPositions[index] = position;

			return false;
		}

		public IEventStoreProvider EnsureExists()
		{
			if (!System.IO.Directory.Exists(Directory))
			{
				System.IO.Directory.CreateDirectory(Directory);
			}

			EventStreams = new EventStreams<byte>() { Directory = Directory };

			return this;
		}

		public IEventStoreProvider Save(EventToStore eventToStore)
		{
			if (0 > eventToStore.Version)
			{
				throw new EventToStoreException("Version must be 0 or greater.") { EventToStore = eventToStore };
			}
			if (null == eventToStore.Data)
			{
				throw new EventToStoreException("Data cannot be null.") { EventToStore = eventToStore };
			}

			if (VersionExists(eventToStore.AggregateRootId, eventToStore.Version))
			{
				throw new ConcurrencyException() { EventToStore = eventToStore, AggregateRootId = eventToStore.AggregateRootId, Version = eventToStore.Version };
			}

			var index = eventToStore.AggregateRootId.ToByteArray()[0];

			Save(EventStreams.Writer(index), eventToStore);

			VersionTracker[eventToStore.AggregateRootId] = eventToStore.Version;

			return this;
		}

		private void Save(BinaryWriter writer, EventToStore eventToStore)
		{
			if (writer.BaseStream.Length != writer.BaseStream.Position)
			{
				writer.BaseStream.Seek(0, SeekOrigin.End);
			}

			writer.Write(eventToStore.AggregateRootId.ToByteArray());
			writer.Write(eventToStore.Data.Length);
			writer.Write(eventToStore.Version);
			writer.Write(eventToStore.Timestamp.Ticks);
			writer.Write(eventToStore.Data);

			writer.Flush();
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			var index = aggregateRootId.ToByteArray()[0];

			var reader = EventStreams.Reader(index);
			var stream = reader.BaseStream;
			var length = stream.Length;
			var position = 0;
			stream.Seek(0, SeekOrigin.Begin);

			while (true)
			{
				if (position >= length)
				{
					length = stream.Length;
					if (position >= length)
					{
						break;
					}
				}

				var savedAggregateId = new Guid(reader.ReadBytes(16));
				var dataSize = reader.ReadInt32();

				if (savedAggregateId != aggregateRootId)
				{
					stream.Seek(sizeof(Int32) + sizeof(Int64) + dataSize, SeekOrigin.Current);
				}
				else
				{

					var version = reader.ReadInt32();
					var timestamp = new DateTime(reader.ReadInt64());

					if (aggregateRootId == savedAggregateId
						&& version >= fromVersion.GetValueOrDefault(-1)
						&& version <= toVersion.GetValueOrDefault(int.MaxValue)
						&& timestamp >= fromTimestamp.GetValueOrDefault(DateTime.MinValue)
						&& timestamp <= toTimestamp.GetValueOrDefault(DateTime.MaxValue))
					{
						yield return new EventToStore()
						{
							AggregateRootId = aggregateRootId,
							Version = version,
							Timestamp = timestamp,
							Data = reader.ReadBytes(dataSize)
						};
					}
					else
					{
						stream.Seek(dataSize, SeekOrigin.Current);
					}
				}

				position += 16 + sizeof(Int32) + sizeof(Int32) + sizeof(Int64) + dataSize;
			}
		}

		public void Dispose()
		{
			if (null != EventStreams)
			{
				EventStreams.Dispose();
				EventStreams = null;
			}
		}

	}

	public class EventStreams<T> : IDisposable
	{
		public EventStreams()
		{
		}

		public string Directory;
		private Dictionary<T, BinaryWriter> Writers = new Dictionary<T, BinaryWriter>();
		private Dictionary<T, BinaryReader> Readers = new Dictionary<T, BinaryReader>();
		public Dictionary<T, long> VersionTrackerLastPositions = new Dictionary<T,long>();

		public BinaryWriter Writer(T index)
		{
			BinaryWriter writer;
			if (!Writers.TryGetValue(index, out writer))
			{
				Writers.Add(index, writer = new BinaryWriter(OpenFile(Path.Combine(Directory, "EventStore_" + index.ToString()), FileAccess.Write, false)));
			}

			return writer;
		}

		public BinaryReader Reader(T index)
		{
			BinaryReader reader;
			if (!Readers.TryGetValue(index, out reader))
			{
				Readers.Add(index, reader = new BinaryReader(OpenFile(Path.Combine(Directory, "EventStore_" + index.ToString()), FileAccess.Read, true)));
			}

			return reader;
		}

		private Stream OpenFile(string name, FileAccess fileAccess, bool buffered)
		{
			return buffered ?
				(Stream) new BufferedStream(File.Open(name, FileMode.OpenOrCreate, fileAccess, FileShare.ReadWrite), 8 * 1024)
				: (Stream) File.Open(name, FileMode.OpenOrCreate, fileAccess, FileShare.ReadWrite);
		}

		public void Dispose()
		{
			foreach (var writer in Writers.Values)
			{
				writer.Close();
			}
			foreach (var reader in Readers.Values)
			{
				reader.Close();
			}
		}
	}
}
