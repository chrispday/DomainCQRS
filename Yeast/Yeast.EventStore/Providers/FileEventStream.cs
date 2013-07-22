using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public class FileEventStream : IDisposable
	{
		private BinaryWriter _writer;
		private Stream _readerStream;
		private BinaryReader _reader;
		private int _versionTracker;
		private Guid _id;

		public FileEventStream(Guid id, string directory, int bufferSize)
		{
			_id = id;
			var idStr = id.ToString();
			var file = GetStreamName(directory, idStr);
			//if (!Directory.Exists(Path.GetDirectoryName(file)))
			//{
			//	Directory.CreateDirectory(Path.GetDirectoryName(file));
			//}
			_writer = new BinaryWriter(File.Open(file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite));
			_writer.Seek(0, SeekOrigin.End);
			_reader = new BinaryReader(_readerStream = new BufferedStream(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), bufferSize));
			_versionTracker = GetLastVersion();
		}

		public void Save(EventToStore eventToStore)
		{
			if (0 > eventToStore.Version)
			{
				throw new EventToStoreException("Version must be 0 or greater.") { EventToStore = eventToStore };
			}
			if (null == eventToStore.Data)
			{
				throw new EventToStoreException("Data cannot be null.") { EventToStore = eventToStore };
			}

			if (eventToStore.Version != _versionTracker + 1)
			{
				throw new ConcurrencyException();
			}

			Write(eventToStore);
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			_readerStream.Seek(0, SeekOrigin.Begin);

			EventToStore eventToStore;
			while (null != (eventToStore = Read(true)))
			{
				if (eventToStore.AggregateRootId == aggregateRootId
					&& eventToStore.Version >= fromVersion.GetValueOrDefault(-1)
					&& eventToStore.Version <= toVersion.GetValueOrDefault(int.MaxValue)
					&& eventToStore.Timestamp >= fromTimestamp.GetValueOrDefault(DateTime.MinValue)
					&& eventToStore.Timestamp <= toTimestamp.GetValueOrDefault(DateTime.MaxValue))
				{
					yield return eventToStore;
				}
			}
		}

		private int GetLastVersion()
		{
			_readerStream.Seek(0, SeekOrigin.End);
			
			EventToStore eventToStore;
			var lastVersion = 0;
			while (null != (eventToStore = Read(false)))
			{
				if (lastVersion < eventToStore.Version)
				{
					lastVersion = eventToStore.Version;
				}
			}
			return lastVersion;
		}

		private EventToStore Read(bool readData)
		{
			//var aggregateRootIdBuf = _reader.ReadBytes(16);
			//if (16 != aggregateRootIdBuf.Length)
			//{
			//	return null;
			//}

			//var aggregateRootId = new Guid(aggregateRootIdBuf);

			var versionBuf = _reader.ReadBytes(sizeof(int));
			if (sizeof(int) != versionBuf.Length)
			{
				return null;
			}

			var version = BitConverter.ToInt32(versionBuf, 0);
			var dataSize = _reader.ReadInt32();
			var timestamp = new DateTime(_reader.ReadInt64());
			byte[] data = null;
			if (readData)
			{
				data = _reader.ReadBytes(dataSize);
			}
			else
			{
				_readerStream.Seek(dataSize, SeekOrigin.Current);
			}

			return new EventToStore()
			{
				AggregateRootId = _id, //aggregateRootId,
				Version = version,
				Timestamp = timestamp,
				Data = data
			};
		}

		private void Write(EventToStore @event)
		{
			byte[] buffer = new byte[/*16 + */sizeof(int) + sizeof(int) + sizeof(long) + @event.Data.Length];
			Array.Copy(@event.AggregateRootId.ToByteArray(), buffer, 16);
			Array.Copy(BitConverter.GetBytes(@event.Version), 0, buffer, /*16*/0, sizeof(int));
			Array.Copy(BitConverter.GetBytes(@event.Data.Length), 0, buffer, /*16 +*/ sizeof(int), sizeof(int));
			Array.Copy(BitConverter.GetBytes(@event.Timestamp.Ticks), 0, buffer, /*16 +*/ sizeof(int) + sizeof(int), sizeof(long));
			Array.Copy(@event.Data, 0, buffer, /*16 +*/ sizeof(int) + sizeof(int) + sizeof(long), @event.Data.Length);

			_writer.Write(buffer);
			_writer.Flush();

			_versionTracker = @event.Version;
		}

		private static string GetStreamName(string directory, string idStr)
		{
			//return Path.Combine(directory, Path.Combine(idStr.Substring(0, 4), Path.Combine(idStr.Substring(4, 4), "EventStream_" + idStr)));
			return Path.Combine(directory,  idStr);
		}

		public void Dispose()
		{
			_writer.Close();
			_reader.Close();
		}
	}
}
