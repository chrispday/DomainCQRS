using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using Yeast.EventStore.Azure.Provider;

namespace Yeast.EventStore
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

namespace Yeast.EventStore.Azure.Provider
{
	public class AzureEventStoreProvider : IEventStoreProvider
	{
		public Common.ILogger Logger { get; set; }
		public string ConnectionString { get; set; }
		private static readonly string EventTable = "Event";
		private static readonly string SubscriberTable = "Subscriber";

		private CloudStorageAccount _storageAccount;
		private CloudTableClient _tableClient;
		private CloudTable _events;
		private CloudTable _subscribers;
		private static readonly int MaximumPropertySize = 64 * 1024 * 1024;
		private static readonly string RowKeyFormat = "D12";

		public IEventStoreProvider EnsureExists()
		{
			_storageAccount = CloudStorageAccount.Parse(ConnectionString);
			_tableClient = _storageAccount.CreateCloudTableClient();

			_events = _tableClient.GetTableReference(EventTable);
			_events.CreateIfNotExists();

			_subscribers = _tableClient.GetTableReference(SubscriberTable);
			_subscribers.CreateIfNotExists();

			return this;
		}

		public IEventStoreProvider Save(EventToStore eventToStore)
		{
			var entity = new DynamicTableEntity(eventToStore.AggregateRootId.ToString(), eventToStore.Version.ToString(RowKeyFormat));
			entity.Properties = SplitData(eventToStore.Data);
			_events.Execute(TableOperation.Insert(entity));
			return this;
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

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			var query = new TableQuery<DynamicTableEntity>()
				.Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, aggregateRootId.ToString()));

			foreach (var result in _events.ExecuteQuery(query))
			{
				var version = int.Parse(result.RowKey);
				var timestamp = result.Timestamp.DateTime;

				if (version >= fromVersion.GetValueOrDefault(-1)
					&& version <= toVersion.GetValueOrDefault(int.MaxValue)
					&& timestamp >= fromTimestamp.GetValueOrDefault(DateTime.MinValue)
					&& timestamp <= toTimestamp.GetValueOrDefault(DateTime.MaxValue))
				{
					var eventToStore = new EventToStore() { AggregateRootId = aggregateRootId, Version = version, Timestamp = timestamp };
					eventToStore.Data = CombineData(result.Properties);
					yield return eventToStore;
				}
			}
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

		public IEventStoreProviderPosition CreatePosition()
		{
			return new AzureEventStoreProviderPosition();
		}

		public IEventStoreProviderPosition LoadPosition(Guid subscriberId)
		{
			var position = new AzureEventStoreProviderPosition();

			var query = new TableQuery<DynamicTableEntity>()
				.Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, subscriberId.ToString()));
			var result = _subscribers.ExecuteQuery(query).FirstOrDefault();

			if (null != result)
			{
				position.Positions = result.Properties.ToDictionary(kvp => new Guid(kvp.Key), kvp => kvp.Value.Int32Value.GetValueOrDefault(0));
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
			tableEntity.Properties = position.Positions.ToDictionary(kvp => kvp.Key.ToString(), kvp => new EntityProperty(kvp.Value));

			_subscribers.Execute(TableOperation.InsertOrReplace(tableEntity));
			return this;
		}

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			return Load(from as AzureEventStoreProviderPosition, to as AzureEventStoreProviderPosition);
		}

		public IEnumerable<EventToStore> Load(AzureEventStoreProviderPosition from, AzureEventStoreProviderPosition to)
		{
			var query = new TableQuery<DynamicTableEntity>()
				.Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, (1).ToString(RowKeyFormat)));

			foreach (var aggregateRootStart in _events.ExecuteQuery(query))
			{
				var aggregateRootId = new Guid(aggregateRootStart.PartitionKey);

				var position = 0;
				from.Positions.TryGetValue(aggregateRootId, out position);

				var aggregateRootQuery = new TableQuery<DynamicTableEntity>()
					.Where(TableQuery.CombineFilters(
						TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, aggregateRootStart.PartitionKey),
						TableOperators.And,
						TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThan, position.ToString(RowKeyFormat))));

				foreach (var result in _events.ExecuteQuery(aggregateRootQuery))
				{
					var eventToStore = new EventToStore() { AggregateRootId = new Guid(result.PartitionKey), Version = int.Parse(result.RowKey), Timestamp = result.Timestamp.DateTime };
					eventToStore.Data = CombineData(result.Properties);
					yield return eventToStore;
					to.Positions[eventToStore.AggregateRootId] = eventToStore.Version;
				}
			}
		}

		public void Dispose()
		{
		}
	}
}
