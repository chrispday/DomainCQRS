using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Yeast.EventStore.Provider.Test
{
	[TestClass]
	public class FileEventStoreProviderTests
	{
		string BaseDirectory;
		Dictionary<Guid, int> LoadTestAggregateIds;
		IEventStoreProvider LoadTestProvider;

		[TestInitialize]
		public void Init()
		{
			BaseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			LoadTestAggregateIds = new Dictionary<Guid,int>();
			foreach (var i in Enumerable.Range(1, 100))
			{
				LoadTestAggregateIds.Add(Guid.NewGuid(), 1);
			}

			LoadTestProvider = new FileEventStoreProvider() { Directory = Path.Combine(BaseDirectory, Guid.NewGuid().ToString()) }.EnsureExists();
		}

		[TestCleanup]
		public void Cleanup()
		{
			if (Directory.Exists(BaseDirectory))
			{
				try
				{
					Directory.Delete(BaseDirectory, true);
				}
				catch { }
			}
		}

		[TestMethod]
		public void FileEventStoreProvider_EnsuresExists()
		{
			var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			try
			{
				var fileEventStoreProvier = new FileEventStoreProvider() { Directory = directory }.EnsureExists();
				Assert.IsTrue(Directory.Exists(directory));
			}
			finally
			{
				if (Directory.Exists(directory))
				{
					Directory.Delete(directory, true);
				}
			}
		}

		[TestMethod]
		public void FileEventStoreProvider_Save()
		{
			var fileEventStoreProvier = new FileEventStoreProvider() { Directory = BaseDirectory }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			fileEventStoreProvier.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 2, Data = new byte[] { 4, 5, 6, 7 } };
			fileEventStoreProvier.Save(EventToStore2);
			(fileEventStoreProvier as FileEventStoreProvider).Dispose();

			var path = Path.Combine(BaseDirectory, "EventStore");
			Assert.IsTrue(File.Exists(path));
			using (var reader = new BinaryReader(File.OpenRead(path)))
			{
				Assert.AreEqual(EventToStore.AggregateId, new Guid(reader.ReadBytes(16)));
				Assert.AreEqual(EventToStore.Version, reader.ReadInt32());
				var size = reader.ReadInt32();
				Assert.AreEqual(EventToStore.Data.Length, size);
				Assert.AreEqual(EventToStore.Timestamp, DateTime.FromBinary(reader.ReadInt64()));
				Assert.IsTrue(EventToStore.Data.SequenceEqual(reader.ReadBytes(size)));
				Assert.AreEqual(EventToStore2.AggregateId, new Guid(reader.ReadBytes(16)));
				Assert.AreEqual(EventToStore2.Version, reader.ReadInt32());
				var size2 = reader.ReadInt32();
				Assert.AreEqual(EventToStore2.Data.Length, size2);
				Assert.AreEqual(EventToStore2.Timestamp, DateTime.FromBinary(reader.ReadInt64()));
				Assert.IsTrue(EventToStore2.Data.SequenceEqual(reader.ReadBytes(size2)));
				Assert.IsTrue(reader.BaseStream.Position == reader.BaseStream.Length);
			}
		}

		[TestMethod, ExpectedException(typeof(ConcurrencyException))]
		public void FileEventStoreProvider_Save_VersionExists()
		{
			var fileEventStoreProvier = new FileEventStoreProvider() { Directory = BaseDirectory }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			fileEventStoreProvier.Save(EventToStore);
			fileEventStoreProvier.Save(EventToStore);
		}

		[TestMethod]
		public void FileEventStoreProvider_Load()
		{
			var fileEventStoreProvier = new FileEventStoreProvider() { Directory = BaseDirectory }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			fileEventStoreProvier.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 2, Data = new byte[] { 4, 5, 6, 7 } };
			fileEventStoreProvier.Save(EventToStore2);
			var EventToStore3 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 3, Data = new byte[] { 1, 3, 5 } };
			fileEventStoreProvier.Save(EventToStore3);

			var events = fileEventStoreProvier.Load(EventToStore.AggregateId, null, null, null, null);
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
		public void FileEventStoreProvider_Load_FromVersion()
		{
			var fileEventStoreProvier = new FileEventStoreProvider() { Directory = BaseDirectory }.EnsureExists();
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			fileEventStoreProvier.Save(EventToStore);
			fileEventStoreProvier.Save(new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 2, Data = new byte[] { 2 } });
			fileEventStoreProvier.Save(new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 3, Data = new byte[] { 3 } });
			fileEventStoreProvier.Save(new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 4, Data = new byte[] { 4 } });

			var events = fileEventStoreProvier.Load(EventToStore.AggregateId, 3, null, null, null);
			Assert.AreEqual(2, events.Count());
			Assert.IsTrue(events.All(se => 3 <= se.Version));
		}

		[TestMethod, ExpectedException(typeof(ConcurrencyException))]
		public void FileEventStoreProvider_LoadAfterSaveOutOfOrder()
		{
			var EventToStore = new EventToStore() { AggregateId = Guid.NewGuid(), Version = 1, Data = new byte[] { 1, 2, 3 } };
			var fileEventStoreProvier = new FileEventStoreProvider() { Directory = BaseDirectory }.EnsureExists();
			var EventToStore3 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 3, Data = new byte[] { 1, 3, 5 } };
			fileEventStoreProvier.Save(EventToStore3);
			var EventToStore2 = new EventToStore() { AggregateId = EventToStore.AggregateId, Version = 2, Data = new byte[] { 4, 5, 6, 7 } };
			fileEventStoreProvier.Save(EventToStore2);
			fileEventStoreProvier.Save(EventToStore);

			var events = fileEventStoreProvier.Load(EventToStore.AggregateId, null, null, null, null);
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
		public void LoadTest_FileEventStoreProvider()
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
