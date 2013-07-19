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

		[TestInitialize]
		public void Init()
		{
			using (var conn = new SqlConnection(ConnectionString))
			{
				conn.Open();
				new SqlCommand("drop table [Event]", conn).ExecuteNonQuery();
			}
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
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			sqlEventEventStoreProvider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Timestamp = DateTime.Now, Data = new byte[] { 4, 5, 6, 7 } };
			sqlEventEventStoreProvider.Save(EventToStore2);

			using (var conn = new SqlConnection(ConnectionString))
			{
				conn.Open();
				using (var reader = new SqlCommand("select [AggregateId], [Version], [Data] from [Event] where [AggregateId] = '" + EventToStore.AggregateRootId.ToString() + "' order by [Version]", conn).ExecuteReader())
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
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			sqlEventEventStoreProvider.Save(EventToStore);
			sqlEventEventStoreProvider.Save(EventToStore);
		}

		[TestMethod]
		public void SqlServerEventStoreProvider_Load()
		{
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			sqlEventEventStoreProvider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Timestamp = DateTime.Now, Data = new byte[] { 4, 5, 6, 7 } };
			sqlEventEventStoreProvider.Save(EventToStore2);
			var EventToStore3 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Timestamp = DateTime.Now, Data = new byte[] { 1, 3, 5 } };
			sqlEventEventStoreProvider.Save(EventToStore3);

			var events = sqlEventEventStoreProvider.Load(EventToStore.AggregateRootId, null, null, null, null);
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
		public void SqlServerEventStoreProvider_Load_FromVersion()
		{
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			sqlEventEventStoreProvider.Save(EventToStore);
			sqlEventEventStoreProvider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Timestamp = DateTime.Now, Data = new byte[] { 2 } });
			sqlEventEventStoreProvider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Timestamp = DateTime.Now, Data = new byte[] { 3 } });
			sqlEventEventStoreProvider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 4, Timestamp = DateTime.Now, Data = new byte[] { 4 } });

			var events = sqlEventEventStoreProvider.Load(EventToStore.AggregateRootId, 3, null, null, null);
			Assert.AreEqual(2, events.Count());
			Assert.IsTrue(events.All(se => 3 <= se.Version));
		}

		[TestMethod]
		public void SqlServerEventStoreProvider_LoadAfterSaveOutOfOrder()
		{
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, Data = new byte[] { 1, 2, 3 } };
			var sqlEventEventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists();
			var EventToStore3 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Timestamp = DateTime.Now, Data = new byte[] { 1, 3, 5 } };
			sqlEventEventStoreProvider.Save(EventToStore3);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Timestamp = DateTime.Now, Data = new byte[] { 4, 5, 6, 7 } };
			sqlEventEventStoreProvider.Save(EventToStore2);
			sqlEventEventStoreProvider.Save(EventToStore);

			var events = sqlEventEventStoreProvider.Load(EventToStore.AggregateRootId, null, null, null ,null);
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
