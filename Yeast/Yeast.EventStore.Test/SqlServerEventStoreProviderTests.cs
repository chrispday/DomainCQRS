using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;

namespace Yeast.EventStore.Provider.Test
{
	[TestClass]
	public class SqlServerEventStoreProviderTests
	{
		string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=EventStore;Integrated Security=True";
		Dictionary<Guid, int> LoadTestAggregateIds;
		SqlServerEventStoreProvider LoadTestProvider;

		[TestInitialize]
		public void Init()
		{
			LoadTestAggregateIds = new Dictionary<Guid, int>();
			foreach (var i in Enumerable.Range(1, 100))
			{
				LoadTestAggregateIds.Add(Guid.NewGuid(), 1);
			}

			using (var conn = new SqlConnection(ConnectionString))
			{
				conn.Open();
				new SqlCommand("drop table [Event]", conn).ExecuteNonQuery();
			}
			LoadTestProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists() as SqlServerEventStoreProvider;
		}

		[TestCleanup]
		public void Cleanup()
		{
		}

		[TestMethod]
		public void SqlServerEventStoreProvider_EnsuresExists()
		{
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			using (var conn = new SqlConnection(ConnectionString))
			{
				conn.Open();
				using (var reader = new SqlCommand("select top 1 * from [Event]", conn).ExecuteReader())
				{
					while (reader.Read()) { }
				}
			}
		}

		[TestMethod]
		public void SqlServerEventStoreProvider_Save()
		{
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			sqlEventEventStoreProvider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 2, Timestamp = DateTime.Now, Data = new byte[] { 4, 5, 6, 7 } };
			sqlEventEventStoreProvider.Save(EventToStore2);

			using (var conn = new SqlConnection(ConnectionString))
			{
				conn.Open();
				using (var reader = new SqlCommand("select [AggregateId], [Version], [Data] from [Event] where [AggregateId] = '" + EventToStore.AggregateId.ToString() + "' order by [Version]", conn).ExecuteReader())
				{
					Assert.IsTrue(reader.Read());
					Assert.AreEqual(EventToStore.Version, reader.GetInt32(1));
					Assert.IsTrue(EventToStore.Data.SequenceEqual(reader.GetFieldValue<byte[]>(2)));

					Assert.IsTrue(reader.Read());
					Assert.AreEqual(EventToStore2.Version, reader.GetInt32(1));
					Assert.IsTrue(EventToStore2.Data.SequenceEqual(reader.GetFieldValue<byte[]>(2)));

					Assert.IsFalse(reader.Read());
				}
			}
		}

		[TestMethod, ExpectedException(typeof(ConcurrencyException))]
		public void SqlServerEventStoreProvider_Save_VersionExists()
		{
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			sqlEventEventStoreProvider.Save(EventToStore);
			sqlEventEventStoreProvider.Save(EventToStore);
		}

		[TestMethod]
		public void SqlServerEventStoreProvider_Load()
		{
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			sqlEventEventStoreProvider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 2, Timestamp = DateTime.Now, Data = new byte[] { 4, 5, 6, 7 } };
			sqlEventEventStoreProvider.Save(EventToStore2);
			var EventToStore3 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 3, Timestamp = DateTime.Now, Data = new byte[] { 1, 3, 5 } };
			sqlEventEventStoreProvider.Save(EventToStore3);

			var events = sqlEventEventStoreProvider.Load(EventToStore.AggregateId, null, null, null, null);
			Assert.AreEqual(3, events.Count());
			var se1 = events.First(se => 1 == se.Version);
			Assert.IsNotNull(se1);
			Assert.AreEqual(EventToStore.AggregateId, se1.AggregateId);
			Assert.AreEqual(EventToStore.Version, se1.Version);
			Assert.IsTrue(EventToStore.Data.SequenceEqual(se1.Data));

			var se2 = events.First(se => 2 == se.Version);
			Assert.IsNotNull(se2);
			Assert.AreEqual(EventToStore2.AggregateId, se2.AggregateId);
			Assert.AreEqual(EventToStore2.Version, se2.Version);
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(se2.Data));

			var se3 = events.First(se => 3 == se.Version);
			Assert.IsNotNull(se3);
			Assert.AreEqual(EventToStore3.AggregateId, se3.AggregateId);
			Assert.AreEqual(EventToStore3.Version, se3.Version);
			Assert.IsTrue(EventToStore3.Data.SequenceEqual(se3.Data));
		}

		[TestMethod]
		public void SqlServerEventStoreProvider_Load_FromVersion()
		{
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			sqlEventEventStoreProvider.Save(EventToStore);
			sqlEventEventStoreProvider.Save(new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 2, Timestamp = DateTime.Now, Data = new byte[] { 2 } });
			sqlEventEventStoreProvider.Save(new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 3, Timestamp = DateTime.Now, Data = new byte[] { 3 } });
			sqlEventEventStoreProvider.Save(new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 4, Timestamp = DateTime.Now, Data = new byte[] { 4 } });

			var events = sqlEventEventStoreProvider.Load(EventToStore.AggregateId, 3, null, null, null);
			Assert.AreEqual(2, events.Count());
			Assert.IsTrue(events.All(se => 3 <= se.Version));
		}

		[TestMethod]
		public void SqlServerEventStoreProvider_LoadAfterSaveOutOfOrder()
		{
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			var EventToStore3 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 3, Timestamp = DateTime.Now, Data = new byte[] { 1, 3, 5 } };
			sqlEventEventStoreProvider.Save(EventToStore3);
			var EventToStore2 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 2, Timestamp = DateTime.Now, Data = new byte[] { 4, 5, 6, 7 } };
			sqlEventEventStoreProvider.Save(EventToStore2);
			sqlEventEventStoreProvider.Save(EventToStore);

			var events = sqlEventEventStoreProvider.Load(EventToStore.AggregateId, null, null, null ,null);
			Assert.AreEqual(3, events.Count());
			var se1 = events.First(se => 1 == se.Version);
			Assert.IsNotNull(se1);
			Assert.AreEqual(EventToStore.AggregateId, se1.AggregateId);
			Assert.AreEqual(EventToStore.Version, se1.Version);
			Assert.IsTrue(EventToStore.Data.SequenceEqual(se1.Data));

			var se2 = events.First(se => 2 == se.Version);
			Assert.IsNotNull(se2);
			Assert.AreEqual(EventToStore2.AggregateId, se2.AggregateId);
			Assert.AreEqual(EventToStore2.Version, se2.Version);
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(se2.Data));

			var se3 = events.First(se => 3 == se.Version);
			Assert.IsNotNull(se3);
			Assert.AreEqual(EventToStore3.AggregateId, se3.AggregateId);
			Assert.AreEqual(EventToStore3.Version, se3.Version);
			Assert.IsTrue(EventToStore3.Data.SequenceEqual(se3.Data));
		}

		[TestMethod]
		public void LoadTest_SqlServerEventStoreProvider()
		{
			foreach (var i in Enumerable.Range(1, 1))
			{
				var id = LoadTestAggregateIds.Keys.ToArray()[new Random().Next(99)];
				var version = LoadTestAggregateIds[id];
				LoadTestAggregateIds[id] = version + 1;
				var eventToStore = new EventToStore() { AggregateId = id, Version = version, Timestamp = DateTime.Now, Data = new Byte[new Random().Next(99)] };
				LoadTestProvider.Save(eventToStore);
			}
		}
	}
}
