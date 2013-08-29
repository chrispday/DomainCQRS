using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Yeast.EventStore.Test
{
	public abstract class EventStoreProviderTestsBase
	{
		protected abstract IEventStoreProvider CreateProvider();
		protected abstract bool ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder { get; }

		[TestMethod]
		public void EventStoreProvider_Save()
		{
			var provider = CreateProvider().EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 4, 5, 6, 7 } };
			provider.Save(EventToStore2);

			var results = provider.Load(EventToStore.AggregateRootId, null, null, null, null).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(EventToStore.AggregateRootId, results[0].AggregateRootId);
			Assert.AreEqual(EventToStore.Version, results[0].Version);
			Assert.AreEqual(EventToStore.EventType, results[0].EventType);
			Assert.IsTrue(EventToStore.Data.SequenceEqual(results[0].Data));
			Assert.AreEqual(EventToStore2.AggregateRootId, results[1].AggregateRootId);
			Assert.AreEqual(EventToStore2.Version, results[1].Version);
			Assert.AreEqual(EventToStore2.EventType, results[0].EventType);
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(results[1].Data));

			provider.Dispose();
		}

		[TestMethod, ExpectedException(typeof(ConcurrencyException))]
		public void EventStoreProvider_Save_VersionExists()
		{
			var provider = CreateProvider().EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			provider.Save(EventToStore);
		}

		[TestMethod]
		public void EventStoreProvider_Load()
		{
			var provider = CreateProvider().EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 4, 5, 6, 7 } };
			provider.Save(EventToStore2);
			var EventToStore3 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 3, 5 } };
			provider.Save(EventToStore3);

			var events = provider.Load(EventToStore.AggregateRootId, null, null, null, null);
			Assert.AreEqual(3, events.Count());
			var se1 = events.First(se => 1 == se.Version);
			Assert.IsNotNull(se1);
			Assert.AreEqual(EventToStore.AggregateRootId, se1.AggregateRootId);
			Assert.AreEqual(EventToStore.Version, se1.Version);
			Assert.AreEqual(EventToStore.EventType, se1.EventType);
			Assert.IsTrue(EventToStore.Data.SequenceEqual(se1.Data));

			var se2 = events.First(se => 2 == se.Version);
			Assert.IsNotNull(se2);
			Assert.AreEqual(EventToStore2.AggregateRootId, se2.AggregateRootId);
			Assert.AreEqual(EventToStore2.Version, se2.Version);
			Assert.AreEqual(EventToStore2.EventType, se2.EventType);
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(se2.Data));

			var se3 = events.First(se => 3 == se.Version);
			Assert.IsNotNull(se3);
			Assert.AreEqual(EventToStore3.AggregateRootId, se3.AggregateRootId);
			Assert.AreEqual(EventToStore3.Version, se3.Version);
			Assert.AreEqual(EventToStore3.EventType, se3.EventType);
			Assert.IsTrue(EventToStore3.Data.SequenceEqual(se3.Data));
		}

		[TestMethod]
		public void EventStoreProvider_LoadUnknownId()
		{
			var provider = CreateProvider().EnsureExists();
			var results = provider.Load(Guid.NewGuid(), null, null, null, null).ToList();
			Assert.AreEqual(0, results.Count);
		}

		[TestMethod]
		public void EventStoreProvider_Load_FromVersion()
		{
			var provider = CreateProvider().EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2 } });
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3 } });
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 4, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 4 } });

			var events = provider.Load(EventToStore.AggregateRootId, 3, null, null, null);
			Assert.AreEqual(2, events.Count());
			Assert.IsTrue(events.All(se => 3 <= se.Version));
		}

		[TestMethod]
		public void EventStoreProvider_LoadAfterSaveOutOfOrder()
		{
			try
			{
				var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
				var provider = CreateProvider().EnsureExists();
				var EventToStore3 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 3, 5 } };
				provider.Save(EventToStore3);
				var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 4, 5, 6, 7 } };
				provider.Save(EventToStore2);
				provider.Save(EventToStore);

				var events = provider.Load(EventToStore.AggregateRootId, null, null, null, null);
				Assert.AreEqual(3, events.Count());
				var se1 = events.First(se => 1 == se.Version);
				Assert.IsNotNull(se1);
				Assert.AreEqual(EventToStore.AggregateRootId, se1.AggregateRootId);
				Assert.AreEqual(EventToStore.Version, se1.Version);
				Assert.AreEqual(EventToStore.EventType, se1.EventType);
				Assert.IsTrue(EventToStore.Data.SequenceEqual(se1.Data));

				var se2 = events.First(se => 2 == se.Version);
				Assert.IsNotNull(se2);
				Assert.AreEqual(EventToStore2.AggregateRootId, se2.AggregateRootId);
				Assert.AreEqual(EventToStore2.Version, se2.Version);
				Assert.AreEqual(EventToStore2.EventType, se2.EventType);
				Assert.IsTrue(EventToStore2.Data.SequenceEqual(se2.Data));

				var se3 = events.First(se => 3 == se.Version);
				Assert.IsNotNull(se3);
				Assert.AreEqual(EventToStore3.AggregateRootId, se3.AggregateRootId);
				Assert.AreEqual(EventToStore3.Version, se3.Version);
				Assert.AreEqual(EventToStore3.EventType, se3.EventType);
				Assert.IsTrue(EventToStore3.Data.SequenceEqual(se3.Data));
			}
			catch (ConcurrencyException)
			{
				if (!ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder)
				{
					throw;
				}
				return;
			}
			if (ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder)
			{
				Assert.Fail("Expected ConcurrencyException.");
			}
		}

		[TestMethod]
		public void EventStoreProvider_Load_MinVersion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, 2, null, null, null).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(2, results[0].Version);
			Assert.AreEqual(3, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MaxVersion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, null, 2, null, null).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(1, results[0].Version);
			Assert.AreEqual(2, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MinMaxVersion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, 2, 2, null, null).ToList();
			Assert.AreEqual(1, results.Count);
			Assert.AreEqual(2, results[0].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MinTimestamp()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 1, Timestamp = new DateTime(2000, 1, 1), EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 2, Timestamp = new DateTime(2000, 1, 2), EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 3, Timestamp = new DateTime(2000, 1, 3), EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, null, null, new DateTime(2000, 1, 2).AddSeconds(-1), null).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(2, results[0].Version);
			Assert.AreEqual(3, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MaxTimestamp()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 1, Timestamp = new DateTime(2000, 1, 1), EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 2, Timestamp = new DateTime(2000, 1, 2), EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 3, Timestamp = new DateTime(2000, 1, 3), EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, null, null, null, new DateTime(2000, 1, 2).AddSeconds(1)).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(1, results[0].Version);
			Assert.AreEqual(2, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MinMaxTimestamp()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 1, Timestamp = new DateTime(2000, 1, 1), EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 2, Timestamp = new DateTime(2000, 1, 2), EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 3, Timestamp = new DateTime(2000, 1, 3), EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, null, null, new DateTime(2000, 1, 2).AddSeconds(-1), new DateTime(2000, 1, 2).AddSeconds(1)).ToList();
			Assert.AreEqual(1, results.Count);
			Assert.AreEqual(2, results[0].Version);
		}

		[TestMethod]
		public void EventStoreProvider_LoadFromPostion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var from = provider.CreatePosition();
			var to = provider.CreatePosition();

			var results = provider.Load(from, to).ToList();
			Assert.AreEqual(3, results.Count);
			Assert.AreEqual(1, results[0].Version);
			Assert.AreEqual(2, results[1].Version);
			Assert.AreEqual(3, results[2].Version);

			provider.Save(new EventToStore() { AggregateRootId = id, Version = 4, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 5, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			from = to;
			to = provider.CreatePosition();

			results = provider.Load(from, to).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(4, results[0].Version);
			Assert.AreEqual(5, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_SaveAndLoadPostion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var from = provider.CreatePosition();
			var to = provider.CreatePosition();

			var results = provider.Load(from, to).ToList();
			Assert.AreEqual(3, results.Count);
			Assert.AreEqual(1, results[0].Version);
			Assert.AreEqual(2, results[1].Version);
			Assert.AreEqual(3, results[2].Version);

			var subId = Guid.NewGuid();
			provider.SavePosition(subId, to);

			provider.Save(new EventToStore() { AggregateRootId = id, Version = 4, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, Version = 5, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			from = provider.LoadPosition(subId);
			to = provider.CreatePosition();

			results = provider.Load(from, to).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(4, results[0].Version);
			Assert.AreEqual(5, results[1].Version);
		}
	}
}
