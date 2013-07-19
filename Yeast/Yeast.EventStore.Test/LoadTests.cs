using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf.Meta;
using ProtoBuf.ServiceModel;
using Yeast.EventStore.Provider;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class LoadTests
	{
		string BaseDirectory;
		Dictionary<Guid, int> LoadTestAggregateIds;
		FileEventStoreProvider FileLoadTestProvider;
		string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=EventStore;Integrated Security=True";
		SqlServerEventStoreProvider SqlLoadTestProvider;

		[TestInitialize]
		public void Init()
		{
			BaseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			LoadTestAggregateIds = new Dictionary<Guid, int>();
			foreach (var i in Enumerable.Range(1, 1000))
			{
				LoadTestAggregateIds.Add(Guid.NewGuid(), 1);
			}

			FileLoadTestProvider = new FileEventStoreProvider() { Directory = Path.Combine(BaseDirectory, Guid.NewGuid().ToString()) }.EnsureExists() as FileEventStoreProvider;

			using (var conn = new SqlConnection(ConnectionString))
			{
				conn.Open();
				new SqlCommand("drop table [Event]", conn).ExecuteNonQuery();
			}
			SqlLoadTestProvider = new SqlServerEventStoreProvider() { ConnectionString = ConnectionString }.EnsureExists() as SqlServerEventStoreProvider;
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
		public void LoadTest_FileEventStoreProvider()
		{
			var stopWatch = Stopwatch.StartNew();

			foreach (var i in Enumerable.Range(1, 1))
			{
				var id = LoadTestAggregateIds.Keys.ToArray()[new Random().Next(99)];
				var version = LoadTestAggregateIds[id];
				LoadTestAggregateIds[id] = version + 1;
				var eventToStore = new EventToStore() { AggregateRootId = id, Version = version, Timestamp = DateTime.Now, Data = new Byte[new Random().Next(99)] };
				FileLoadTestProvider.Save(eventToStore);
			}

			stopWatch.Stop();
			Debug.WriteLine("Time taken {0}", stopWatch.Elapsed);
		}

		[TestMethod]
		public void LoadTest_SqlServerEventStoreProvider()
		{
			var stopWatch = Stopwatch.StartNew();

			foreach (var i in Enumerable.Range(1, 1))
			{
				var id = LoadTestAggregateIds.Keys.ToArray()[new Random().Next(99)];
				var version = LoadTestAggregateIds[id];
				LoadTestAggregateIds[id] = version + 1;
				var eventToStore = new EventToStore() { AggregateRootId = id, Version = version, Timestamp = DateTime.Now, Data = new Byte[new Random().Next(99)] };
				SqlLoadTestProvider.Save(eventToStore);
			}

			stopWatch.Stop();
			Debug.WriteLine("Time taken {0}", stopWatch.Elapsed);
		}

		[TestMethod]
		public void LoadTest_MessageReceiver_FileStore()
		{
			var typeModel = RuntimeTypeModel.Create();
			typeModel.Add(typeof(MockCommand), true);
			var serializer = new XmlProtoSerializer(typeModel, typeof(MockCommand));
			var eventStore = new EventStore() { Serializer = new XmlObjectSerializer() { Serializer = serializer }, EventStoreProvider = FileLoadTestProvider };
			var random = new Random();

			var eventReceiver = new MessageReceiver() { EventStore = eventStore }
				.Register<MockCommand, MockAggregateRoot>()
				.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

			var id = LoadTestAggregateIds.Keys.ToArray()[new Random().Next(999)];
			eventReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = random.Next(10) - 5 });

			var stopWatch = Stopwatch.StartNew();

			var amount = 10000;
			foreach (var i in Enumerable.Range(1, amount))
			{
				id = LoadTestAggregateIds.Keys.ToArray()[new Random().Next(999)];
				eventReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = random.Next(10) - 5 });
			}

			stopWatch.Stop();
			
			Debug.WriteLine("Time taken {0}", stopWatch.Elapsed);
			Debug.WriteLine("Per sec {0:#,##0.0}", amount / stopWatch.Elapsed.TotalSeconds);
			Debug.WriteLine("Files in Event Store {0}", Directory.GetFiles(FileLoadTestProvider.Directory, "*.*", SearchOption.AllDirectories).Count());
			Debug.WriteLine("Size of Event Store {0:#,##0.0} KB", Directory.GetFiles(FileLoadTestProvider.Directory, "*.*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / 1024.0);
			Debug.WriteLine("Avg Size of Event Store {0:#,##0.0} KB", Directory.GetFiles(FileLoadTestProvider.Directory, "*.*", SearchOption.AllDirectories).Average(f => new FileInfo(f).Length) / 1024.0);
		}
	}
}
