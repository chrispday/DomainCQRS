using System;
using System.Collections.Generic;
using System.IO;

using System.Text;
using DomainCQRS.Common;
using DomainCQRS.Persister;

namespace DomainCQRS.Persister
{
	/// <summary>
	/// Persists event for an Aggregate Root to a file.
	/// </summary>
	public class FileEventStream : IDisposable
	{
		private BinaryWriter _writer;
		private Stream _readerStream;
		private BinaryReader _reader;
		private Stream _publisherStream;
		private BinaryReader _publisherReader;
		private long _publisherPosition;
		private int _versionTracker;
		private Guid _id;
		private string _name;
		private int _bufferSize;
		private bool _storeAggregateId;

		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		public string Name { get { return _name; } }

		public FileEventStream(ILogger logger, Guid id, string directory, int bufferSize, bool publishingOnly, bool storeAggregateId)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == directory)
			{
				throw new ArgumentNullException(directory);
			}
			if (0 >= bufferSize)
			{
				throw new ArgumentOutOfRangeException("bufferSize");
			}

			_logger = logger;
			_id = id;
			_bufferSize = bufferSize;
			_name = GetName(directory, id);
			_storeAggregateId = storeAggregateId;

			if (publishingOnly)
			{
				_writer = new BinaryWriter(File.Open(_name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite));
				_writer.Seek(0, SeekOrigin.End);
				_reader = new BinaryReader(_readerStream = new BufferedStream(File.Open(_name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), _bufferSize));
				_versionTracker = GetLastVersion();
			}

			Logger.Verbose("Creating for id {0} stream {1} with last version {2}", id, "", _versionTracker);
		}

		public void Save(EventToStore eventToStore)
		{
			if (null == eventToStore)
			{
				throw new ArgumentNullException("eventToStore");
			}
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
			while (null != (eventToStore = Read(_readerStream, _reader, aggregateRootId, true)))
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

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, FileEventPersisterPosition from, FileEventPersisterPosition to)
		{
			if (null == to)
			{
				throw new ArgumentNullException("to");
			}

			if (null == _publisherReader)
			{
				lock (this)
				{
					if (null == _publisherReader)
					{
						_publisherReader = new BinaryReader(_publisherStream = new BufferedStream(File.Open(_name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), _bufferSize));
						_publisherPosition = 0;
					}
				}
			}

			long fromPosition = 0;
			if (null != from)
			{
				from.Positions.TryGetValue(aggregateRootId, out fromPosition);
			}

			lock (_publisherStream)
			{
				if (_publisherPosition != fromPosition)
				{
					_publisherPosition = _publisherStream.Seek(fromPosition, SeekOrigin.Begin);
				}
				long toPosition;
				if (!to.Positions.TryGetValue(aggregateRootId, out toPosition))
				{
					toPosition = _publisherStream.Length;
				}

				Logger.Verbose("Publishing from {1} to {2} for {0}", aggregateRootId, fromPosition, toPosition);

				FileEventToStore eventToStore;
				while ((_publisherPosition < toPosition)
					&& (null != (eventToStore = Read(_publisherStream, _publisherReader, aggregateRootId, true))))
				{
					to.Positions[aggregateRootId] = _publisherPosition += eventToStore.Size;
					yield return eventToStore;
				}

				to.Positions[aggregateRootId] = _publisherPosition;
			}
		}

		private int GetLastVersion()
		{
			_readerStream.Seek(0, SeekOrigin.Begin);
			
			EventToStore eventToStore;
			var lastVersion = 0;
			while (null != (eventToStore = Read(_readerStream, _reader, Guid.Empty, false)))
			{
				if (lastVersion < eventToStore.Version)
				{
					lastVersion = eventToStore.Version;
				}
			}
			return lastVersion;
		}

		private FileEventToStore Read(Stream readerStream, BinaryReader reader, Guid aggregateRootId, bool readData)
		{
			if (_storeAggregateId)
			{
				var aggregateRootIdBuf = _reader.ReadBytes(16);
				if (16 != aggregateRootIdBuf.Length)
				{
					return null;
				}
				aggregateRootId = new Guid(aggregateRootIdBuf);
			}

			var versionBuf = reader.ReadBytes(sizeof(int));
			if (sizeof(int) != versionBuf.Length)
			{
				return null;
			}

			var version = BitConverter.ToInt32(versionBuf, 0);
			var aggregateRootTypeSize = reader.ReadInt32();
			var eventTypeSize = reader.ReadInt32();
			var dataSize = reader.ReadInt32();
			var timestamp = new DateTime(reader.ReadInt64());
			var aggregateRootType = new byte[] { };
			var eventType = new byte[] { };
			byte[] data = null;
			if (readData)
			{
				aggregateRootType = reader.ReadBytes(aggregateRootTypeSize);
				eventType = reader.ReadBytes(eventTypeSize);
				data = reader.ReadBytes(dataSize);
			}
			else
			{
				readerStream.Seek(aggregateRootTypeSize + eventTypeSize + dataSize, SeekOrigin.Current);
			}

			return new FileEventToStore()
			{
				AggregateRootId = aggregateRootId,
				AggregateRootType = Encoding.UTF8.GetString(aggregateRootType),
				Version = version,
				Timestamp = timestamp,
				EventType = Encoding.UTF8.GetString(eventType),
				Data = data,
				Size = sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(long) + aggregateRootTypeSize + eventTypeSize + dataSize + (_storeAggregateId ? 16 : 0)
			};
		}

		private void Write(EventToStore @event)
		{
			var guidOffset = _storeAggregateId ? 16 : 0;

			byte[] aggregateRootType = Encoding.UTF8.GetBytes(@event.AggregateRootType);
			byte[] eventType = Encoding.UTF8.GetBytes(@event.EventType);

			byte[] buffer = new byte[guidOffset + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(long) + aggregateRootType.Length + eventType.Length + @event.Data.Length];
			if (_storeAggregateId)
			{
				Array.Copy(@event.AggregateRootId.ToByteArray(), buffer, 16);
			}
			Array.Copy(BitConverter.GetBytes(@event.Version), 0, buffer, guidOffset, sizeof(int));
			Array.Copy(BitConverter.GetBytes(aggregateRootType.Length), 0, buffer, guidOffset + sizeof(int), sizeof(int));
			Array.Copy(BitConverter.GetBytes(eventType.Length), 0, buffer, guidOffset + sizeof(int) + sizeof(int), sizeof(int));
			Array.Copy(BitConverter.GetBytes(@event.Data.Length), 0, buffer, guidOffset + sizeof(int) + sizeof(int) + sizeof(int), sizeof(int));
			Array.Copy(BitConverter.GetBytes(@event.Timestamp.Ticks), 0, buffer, guidOffset + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int), sizeof(long));
			Array.Copy(aggregateRootType, 0, buffer, guidOffset + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(long), aggregateRootType.Length);
			Array.Copy(eventType, 0, buffer, guidOffset + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(long) + aggregateRootType.Length, eventType.Length);
			Array.Copy(@event.Data, 0, buffer, guidOffset + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(long) + aggregateRootType.Length + eventType.Length, @event.Data.Length);

			_writer.Write(buffer);
			_writer.Flush();

			_versionTracker = @event.Version;
		}

		private string GetName(string directory, Guid id)
		{
			return Path.Combine(directory,  id.ToString());
		}

		public void Dispose()
		{
			Logger.Verbose("Disposing for {0} stream {1} last version {2}", _id, _name, _versionTracker);
			_writer.Close();
			_reader.Close();
			if (null != _publisherReader)
			{
				_publisherReader.Close();
			}
		}
	}
}
