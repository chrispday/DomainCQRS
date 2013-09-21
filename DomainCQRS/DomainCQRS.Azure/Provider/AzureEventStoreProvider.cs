using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using DomainCQRS.Azure.Provider;

namespace DomainCQRS
{
	public static class AzureEventStoreProviderConfigure
	{
		public static IConfigure AzureEventStoreProvider(this IConfigure configure, string connectionString)
		{
			var c = configure as Configure;
			c.EventStoreProvider = new AzureEventStoreProvider() { ConnectionString = connectionString, Logger = c.Logger }.EnsureExists();
			return configure;
		}
	}
}

namespace DomainCQRS.Azure.Provider
{
	public class AzureEventStoreProvider : IEventStoreProvider
	{
		public Common.ILogger Logger { get; set; }
		public string ConnectionString { get; set; }
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

		public IEventStoreProvider EnsureExists()
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

		public IEventStoreProvider Save(EventToStore eventToStore)
		{
			var entity = new DynamicTableEntity(eventToStore.AggregateRootId.ToString(), "");
			_aggregateRootIds.Execute(TableOperation.InsertOrMerge(entity));

			entity = new DynamicTableEntity(eventToStore.AggregateRootId.ToString(), eventToStore.Version.ToString(RowKeyFormat));
			entity.Properties = SplitData(eventToStore.Data);
			entity.Properties["_Timestamp"] = new EntityProperty(eventToStore.Timestamp);
			entity.Properties["EventType"] = new EntityProperty(eventToStore.EventType);
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

		public IEventStoreProviderPosition CreatePosition()
		{
			return new AzureEventStoreProviderPosition();
		}

		public IEventStoreProviderPosition LoadPosition(Guid subscriberId)
		{
			var position = new AzureEventStoreProviderPosition();

			var query = new TableQuery<DynamicTableEntity>()
				.Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, subscriberId.ToString()));

			foreach (var result in _subscribers.ExecuteQuery(query))
			{
				position.Positions[new Guid(result.RowKey)] = result.Properties["Position"].Int32Value.GetValueOrDefault(0);
			}

			return position;
		}

		public IEventStoreProvider SavePosition(Guid subscriberId, IEventStoreProviderPosition position)
		{
			return SavePosition(subscriberId, position as AzureEventStoreProviderPosition);
 		}

		public IEventStoreProvider SavePosition(Guid subscriberId, AzureEventStoreProviderPosition position)
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

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			return Load(from as AzureEventStoreProviderPosition, to as AzureEventStoreProviderPosition);
		}

		public IEnumerable<EventToStore> Load(AzureEventStoreProviderPosition from, AzureEventStoreProviderPosition to)
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
			var eventToStore = new EventToStore() { AggregateRootId = new Guid(result.PartitionKey), Version = int.Parse(result.RowKey), Timestamp = result.Timestamp.DateTime, EventType = result.Properties["EventType"].StringValue };
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
