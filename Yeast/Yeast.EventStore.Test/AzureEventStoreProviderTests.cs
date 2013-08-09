using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Yeast.EventStore.Common;
using Microsoft.WindowsAzure.Storage;
using Yeast.EventStore.Azure.Provider;
using Microsoft.WindowsAzure.Storage.Table;

namespace Yeast.EventStore.Provider.Test
{
	[TestClass]
	public class AzureEventStoreProviderTests
	{
		public string ConnectionString = "UseDevelopmentStorage=true";
		private static readonly string EventTable = "Event";
		private static readonly string SubscriberTable = "Subscriber";
		private static readonly string AggregateRootIdsTable = "AggregateRootIds";

		[TestInitialize]
		public void Init()
		{
		}

		[TestCleanup]
		public void Cleanup()
		{
			var _storageAccount = CloudStorageAccount.Parse(ConnectionString);
			var _tableClient = _storageAccount.CreateCloudTableClient();

			var _events = _tableClient.GetTableReference(EventTable);
			_events.DeleteIfExists();

			var _aggregateRootIds = _tableClient.GetTableReference(AggregateRootIdsTable);
			_aggregateRootIds.DeleteIfExists();

			var _subscribers = _tableClient.GetTableReference(SubscriberTable);
			_subscribers.DeleteIfExists();
		}

		[TestMethod]
		public void AzureEventStoreProvider_Save()
		{
			var provider = new AzureEventStoreProvider() { ConnectionString = ConnectionString, Logger = new DebugLogger() }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Data = new byte[] { 4, 5, 6, 7 } };
			provider.Save(EventToStore2);
			(provider as AzureEventStoreProvider).Dispose();

			var _storageAccount = CloudStorageAccount.Parse(ConnectionString);
			var _tableClient = _storageAccount.CreateCloudTableClient();
			var _events = _tableClient.GetTableReference(EventTable);
			var results = _events.ExecuteQuery(new TableQuery<DynamicTableEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, EventToStore.AggregateRootId.ToString()))).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(EventToStore.AggregateRootId, new Guid(results[0].PartitionKey));
			Assert.AreEqual(EventToStore.Version, int.Parse(results[0].RowKey));
			Assert.IsTrue(EventToStore.Data.SequenceEqual(results[0].Properties["Data0"].BinaryValue));
			Assert.AreEqual(EventToStore2.AggregateRootId, new Guid(results[1].PartitionKey));
			Assert.AreEqual(EventToStore2.Version, int.Parse(results[1].RowKey));
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(results[1].Properties["Data0"].BinaryValue));
		}

		[TestMethod, ExpectedException(typeof(ConcurrencyException))]
		public void AzureEventStoreProvider_Save_VersionExists()
		{
			var provider = new AzureEventStoreProvider() { ConnectionString = ConnectionString, Logger = new DebugLogger() }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			provider.Save(EventToStore);
		}

		[TestMethod]
		public void AzureEventStoreProvider_Load()
		{
			var provider = new AzureEventStoreProvider() { ConnectionString = ConnectionString, Logger = new DebugLogger() }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Data = new byte[] { 4, 5, 6, 7 } };
			provider.Save(EventToStore2);
			var EventToStore3 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Data = new byte[] { 1, 3, 5 } };
			provider.Save(EventToStore3);

			var events = provider.Load(EventToStore.AggregateRootId, null, null, null, null);
			Assert.AreEqual(3, events.Count());
			var se1 = events.First(se => 1 == se.Version);
			Assert.IsNotNull(se1);
			Assert.AreEqual(EventToStore.AggregateRootId, se1.AggregateRootId);
			Assert.AreEqual(EventToStore.Version, se1.Version);
			Assert.IsTrue(EventToStore.Data.SequenceEqual(se1.Data));

			var se2 = events.First(se => 2 == se.Version);
			Assert.IsNotNull(se2);
			Assert.AreEqual(EventToStore2.AggregateRootId, se2.AggregateRootId);
			Assert.AreEqual(EventToStore2.Version, se2.Version);
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(se2.Data));

			var se3 = events.First(se => 3 == se.Version);
			Assert.IsNotNull(se3);
			Assert.AreEqual(EventToStore3.AggregateRootId, se3.AggregateRootId);
			Assert.AreEqual(EventToStore3.Version, se3.Version);
			Assert.IsTrue(EventToStore3.Data.SequenceEqual(se3.Data));
		}

		[TestMethod]
		public void AzureEventStoreProvider_Load_FromVersion()
		{
			var provider = new AzureEventStoreProvider() { ConnectionString = ConnectionString, Logger = new DebugLogger() }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Data = new byte[] { 2 } });
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Data = new byte[] { 3 } });
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 4, Data = new byte[] { 4 } });

			var events = provider.Load(EventToStore.AggregateRootId, 3, null, null, null);
			Assert.AreEqual(2, events.Count());
			Assert.IsTrue(events.All(se => 3 <= se.Version));
		}

		[TestMethod]
		public void AzureEventStoreProvider_LoadAfterSaveOutOfOrder()
		{
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			var provider = new AzureEventStoreProvider() { ConnectionString = ConnectionString, Logger = new DebugLogger() }.EnsureExists();
			var EventToStore3 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Data = new byte[] { 1, 3, 5 } };
			provider.Save(EventToStore3);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Data = new byte[] { 4, 5, 6, 7 } };
			provider.Save(EventToStore2);
			provider.Save(EventToStore);

			var events = provider.Load(EventToStore.AggregateRootId, null, null, null, null);
			Assert.AreEqual(3, events.Count());
			var se1 = events.First(se => 1 == se.Version);
			Assert.IsNotNull(se1);
			Assert.AreEqual(EventToStore.AggregateRootId, se1.AggregateRootId);
			Assert.AreEqual(EventToStore.Version, se1.Version);
			Assert.IsTrue(EventToStore.Data.SequenceEqual(se1.Data));

			var se2 = events.First(se => 2 == se.Version);
			Assert.IsNotNull(se2);
			Assert.AreEqual(EventToStore2.AggregateRootId, se2.AggregateRootId);
			Assert.AreEqual(EventToStore2.Version, se2.Version);
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(se2.Data));

			var se3 = events.First(se => 3 == se.Version);
			Assert.IsNotNull(se3);
			Assert.AreEqual(EventToStore3.AggregateRootId, se3.AggregateRootId);
			Assert.AreEqual(EventToStore3.Version, se3.Version);
			Assert.IsTrue(EventToStore3.Data.SequenceEqual(se3.Data));
		}
	}
}
