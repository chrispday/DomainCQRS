using System;
using System.Collections.Generic;
using System.IO;

using Yeast.EventStore.Common;

namespace Yeast.EventStore.Provider
{
	public class FileEventStoreProvider : IEventStoreProvider, IDisposable
	{
		public string Directory { get; set; }
		public TimeSpan LockTimeout { get; set; }
		public int OpenFileRetryCount { get; set; }
		public int VersionTrackerCapacity { get; set; }
		private BinaryWriter EventWriter;
		private Stream VersionReaderStream;
		private BinaryReader VersionReader;
		private LRUDictionary<Guid, int> VersionTracker;
		private long VersionTrackerLastPosition;
		private const short Check = 666;

		public FileEventStoreProvider()
		{
			LockTimeout = TimeSpan.FromSeconds(5);
			OpenFileRetryCount = 5;
			VersionTrackerCapacity = 10000;
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

			if (null == VersionReader)
			{
				VersionReaderStream = OpenFile(FileAccess.Read);
				VersionReader = new BinaryReader(VersionReaderStream);
			}

			if (VersionReaderStream.Position != VersionTrackerLastPosition)
			{
				VersionReaderStream.Seek(VersionTrackerLastPosition, SeekOrigin.Begin);
			}

			var streamLength = VersionReaderStream.Length;
			while (true)
			{
				if (VersionReaderStream.Position >= streamLength) { break; }
				var savedAggregateId = new Guid(VersionReader.ReadBytes(16));
				int version = VersionReader.ReadInt32();
				int dataSize = VersionReader.ReadInt32();
				VersionReader.ReadBytes(dataSize + sizeof(Int64));

				VersionTracker[savedAggregateId] = version;

				if (aggregateRootId == savedAggregateId
					&& version == versionToCheck)
				{
					VersionTrackerLastPosition = VersionReaderStream.Position;
					return true;
				}
			}

			VersionTrackerLastPosition = VersionReaderStream.Position;

			return false;
		}

		private Stream OpenFile(FileAccess fileAccess)
		{
			return new BufferedStream(File.Open(Path.Combine(Directory, "EventStore"), FileMode.OpenOrCreate, fileAccess, FileShare.ReadWrite), 4 * 1024);
		}

		public IEventStoreProvider EnsureExists()
		{
			if (!System.IO.Directory.Exists(Directory))
			{
				System.IO.Directory.CreateDirectory(Directory);
			}

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

			if (null == EventWriter)
			{
				EventWriter = new BinaryWriter(OpenFile(FileAccess.Write));
			}

			if (VersionExists(eventToStore.AggregateRootId, eventToStore.Version))
			{
				throw new ConcurrencyException() { EventToStore = eventToStore, AggregateRootId = eventToStore.AggregateRootId, Version = eventToStore.Version };
			}

			if (EventWriter.BaseStream.Length != EventWriter.BaseStream.Position)
			{
				EventWriter.Seek(0, SeekOrigin.End);
			}

			EventWriter.Write(eventToStore.AggregateRootId.ToByteArray());
			EventWriter.Write(eventToStore.Version);
			EventWriter.Write(eventToStore.Data.Length);
			EventWriter.Write(eventToStore.Timestamp.ToBinary());
			EventWriter.Write(eventToStore.Data);
			EventWriter.Flush();

			VersionTracker[eventToStore.AggregateRootId] = eventToStore.Version;

			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			using (var reader = new BinaryReader(OpenFile(FileAccess.Read)))
			{
				while (true)
				{
					if (reader.BaseStream.Position == reader.BaseStream.Length) { break; }
					var savedAggregateId = new Guid(reader.ReadBytes(16));
					var version = reader.ReadInt32();
					var dataSize = reader.ReadInt32();
					var timestamp = DateTime.FromBinary(reader.ReadInt64());
					var data = reader.ReadBytes(dataSize);

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
							Data = data
						};
					}
				}
			}
		}

		public void Dispose()
		{
			if (null != EventWriter)
			{
				EventWriter.Close();
				EventWriter = null;
			}
			if (null != VersionReader)
			{
				VersionReader.Close();
				VersionReader = null;
			}
		}
	}
}
