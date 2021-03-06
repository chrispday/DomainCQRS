﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using DomainCQRS.Azure.Persister;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class AzureEventPersisterConfigure
	{
		public static IConfigure AzureEventPersister(this IConfigure configure, string connectionString)
		{
			configure.Registry
				.BuildInstancesOf<IEventPersister>()
				.TheDefaultIs(Registry.Instance<IEventPersister>()
					.UsingConcreteType<AzureEventPersister>()
					.WithProperty("connectionString").EqualTo(connectionString))
				.AsSingletons();
			return configure;
		}
	}
}

namespace DomainCQRS.Azure.Persister
{
	public class AzureEventPersister : IEventPersister
	{
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		private readonly string _connectionString;
		public string ConnectionString { get { return _connectionString; } }

		private static readonly string EventTable = "Event";
		private static readonly string AggregateRootIdsTable = "AggregateRootIds";
		private static readonly string SubscriberTable = "Subscriber";

		private CloudStorageAccount _storageAccount;
		private CloudTableClient _tableClient;
		private CloudTable _events;
		private CloudTable _aggregateRootIds;
		private CloudTable _subscribers;
		private static readonly int MaximumPropertySize = 64 * 1024 * 1024;
		private static readonly string RowKeyFormat = "D12";

		public AzureEventPersister(ILogger logger, string connectionString)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == connectionString)
			{
				throw new ArgumentNullException("connectionString");
			}

			_logger = logger;
			_connectionString = connectionString;
		}

		public IEventPersister EnsureExists()
		{
			_storageAccount = CloudStorageAccount.Parse(ConnectionString);
			_tableClient = _storageAccount.CreateCloudTableClient();

			_events = _tableClient.GetTableReference(EventTable);
			_events.CreateIfNotExists();

			_aggregateRootIds = _tableClient.GetTableReference(AggregateRootIdsTable);
			_aggregateRootIds.CreateIfNotExists();

			_subscribers = _tableClient.GetTableReference(SubscriberTable);
			_subscribers.CreateIfNotExists();

			return this;
		}

		public IEventPersister Save(EventToStore eventToStore)
		{
			var entity = new DynamicTableEntity(eventToStore.AggregateRootId.ToString(), "");
			_aggregateRootIds.Execute(TableOperation.InsertOrMerge(entity));

			entity = new DynamicTableEntity(eventToStore.AggregateRootId.ToString(), eventToStore.Version.ToString(RowKeyFormat));
			entity.Properties = SplitData(eventToStore.Data);
			entity.Properties["_Timestamp"] = new EntityProperty(eventToStore.Timestamp);
			entity.Properties["EventType"] = new EntityProperty(eventToStore.EventType);
			entity.Properties["AggregateRootType"] = new EntityProperty(eventToStore.AggregateRootType);
			try
			{
				_events.Execute(TableOperation.Insert(entity));
			}
			catch (StorageException ex)
			{
				if (ex.Message.Contains("409"))
				{
					throw new ConcurrencyException();
				}
				throw;
			}
			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			var query = new TableQuery<DynamicTableEntity>()
				.Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, aggregateRootId.ToString()));

			foreach (var result in _events.ExecuteQuery(query))
			{
				var version = int.Parse(result.RowKey);
				var timestamp = result.Properties["_Timestamp"].DateTimeOffsetValue.Value.DateTime;

				if (version >= fromVersion.GetValueOrDefault(-1)
					&& version <= toVersion.GetValueOrDefault(int.MaxValue)
					&& timestamp >= fromTimestamp.GetValueOrDefault(DateTime.MinValue)
					&& timestamp <= toTimestamp.GetValueOrDefault(DateTime.MaxValue))
				{
					yield return CreateEventToStore(result);
				}
			}
		}

		public IEventPersisterPosition CreatePosition()
		{
			return new AzureEventPersisterPosition();
		}

		public IEventPersisterPosition LoadPosition(Guid subscriberId)
		{
			var position = new AzureEventPersisterPosition();

			var query = new TableQuery<DynamicTableEntity>()
				.Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, subscriberId.ToString()));

			foreach (var result in _subscribers.ExecuteQuery(query))
			{
				position.Positions[new Guid(result.RowKey)] = result.Properties["Position"].Int32Value.GetValueOrDefault(0);
			}

			return position;
		}

		public IEventPersister SavePosition(Guid subscriberId, IEventPersisterPosition position)
		{
			return SavePosition(subscriberId, position as AzureEventPersisterPosition);
 		}

		public IEventPersister SavePosition(Guid subscriberId, AzureEventPersisterPosition position)
		{
			var tableEntity = new DynamicTableEntity(subscriberId.ToString(), "");

			foreach (var p in position.Positions)
			{
				tableEntity.RowKey = p.Key.ToString();
				tableEntity.Properties["Position"] = new EntityProperty(p.Value);
				_subscribers.Execute(TableOperation.InsertOrReplace(tableEntity));
			}

			return this;
		}

		public IEnumerable<EventToStore> Load(IEventPersisterPosition from, IEventPersisterPosition to)
		{
			return Load(from as AzureEventPersisterPosition, to as AzureEventPersisterPosition);
		}

		public IEnumerable<EventToStore> Load(AzureEventPersisterPosition from, AzureEventPersisterPosition to)
		{
			Logger.Verbose("from {0} to {1}", from, to);

			var query = new TableQuery<DynamicTableEntity>();

			foreach (var aggregateRootKey in _aggregateRootIds.ExecuteQuery(query))
			{
				var aggregateRootId = new Guid(aggregateRootKey.PartitionKey);

				var position = 0;
				from.Positions.TryGetValue(aggregateRootId, out position);

				var aggregateRootQuery = new TableQuery<DynamicTableEntity>()
					.Where(TableQuery.CombineFilters(
						TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, aggregateRootKey.PartitionKey),
						TableOperators.And,
						TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThan, position.ToString(RowKeyFormat))));

				foreach (var result in _events.ExecuteQuery(aggregateRootQuery))
				{
					var eventToStore = CreateEventToStore(result);
					to.Positions[aggregateRootId] = eventToStore.Version;
					yield return eventToStore;
				}

				if (!to.Positions.ContainsKey(aggregateRootId)
					&& from.Positions.ContainsKey(aggregateRootId))
				{
					to.Positions[aggregateRootId] = from.Positions[aggregateRootId];
				}
			}
		}

		protected IDictionary<string, EntityProperty> SplitData(byte[] eventData)
		{
			var properties = new Dictionary<string, EntityProperty>();

			var data = new byte[MaximumPropertySize];
			var dataCount = 0;
			var iTotal = 0;
			var i = 0;
			while (iTotal < eventData.Length)
			{
				if (i >= MaximumPropertySize)
				{
					properties["Data" + dataCount++] = new EntityProperty(data);
					i = 0;
				}

				data[i++] = eventData[iTotal++];
			}

			properties["Data" + dataCount] = new EntityProperty(data.Take(i).ToArray());

			return properties;
		}

		private EventToStore CreateEventToStore(DynamicTableEntity result)
		{
			var eventToStore = new EventToStore() 
			{ 
				AggregateRootId = new Guid(result.PartitionKey), 
				Version = int.Parse(result.RowKey), 
				Timestamp = result.Timestamp.DateTime, 
				EventType = result.Properties["EventType"].StringValue
			};
			if (result.Properties.ContainsKey("AggregateRootType"))
			{
				eventToStore.AggregateRootType = result.Properties["AggregateRootType"].StringValue;
			}
			eventToStore.Data = CombineData(result.Properties);
			return eventToStore;
		}

		private byte[] CombineData(IDictionary<string, EntityProperty> dictionary)
		{
			var data = new List<byte[]>();
			var i = 0;
			EntityProperty entityProperty;
			while (dictionary.TryGetValue("Data" + i++, out entityProperty))
			{
				data.Add(entityProperty.BinaryValue);
			}

			return (1 == data.Count) ? data[0] : data.SelectMany(d => d).ToArray();
		}

		public void Dispose()
		{
		}
	}
}
