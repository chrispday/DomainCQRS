using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf.Meta;
using ProtoBuf.ServiceModel;
using DomainCQRS.Common;
using DomainCQRS.Provider;
using DomainCQRS.Test.Mock;

namespace DomainCQRS.Test
{
	//[TestClass]
	public class LoadTests
	{
		string BaseDirectory;
		Dictionary<Guid, int> LoadTestAggregateIds;
		string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=EventStore;Integrated Security=True";
		SqlServerEventStoreProvider SqlLoadTestProvider;

		[TestInitialize]
		public void Init()
		{
			BaseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			LoadTestAggregateIds = new Dictionary<Guid, int>();
			foreach (var i in Enumerable.Range(1, 100000))
			{
				LoadTestAggregateIds.Add(Guid.NewGuid(), 1);
			}


			try
			{
				using (var conn = new SqlConnection(ConnectionString))
				{
					conn.Open();
					new SqlCommand("drop table [Event]", conn).ExecuteNonQuery();
					new SqlCommand("drop table [Subscriber]", conn).ExecuteNonQuery();
				}
			}
			catch { }
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
			var fileLoadTestProvider = new FileEventStoreProvider() { Directory = Path.Combine(BaseDirectory, Guid.NewGuid().ToString()), Logger = new DebugLogger() }.EnsureExists() as FileEventStoreProvider;

			var stopWatch = Stopwatch.StartNew();

			foreach (var i in Enumerable.Range(1, 1))
			{
				var id = LoadTestAggregateIds.Keys.ToArray()[Ran(new Random(), LoadTestAggregateIds.Count - 1)];
				var version = LoadTestAggregateIds[id];
				LoadTestAggregateIds[id] = version + 1;
				var eventToStore = new EventToStore() { AggregateRootId = id, Version = version, Timestamp = DateTime.Now, EventType = typeof(byte[]).AssemblyQualifiedName, Data = new Byte[new Random().Next(99)] };
				fileLoadTestProvider.Save(eventToStore);
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
				var id = LoadTestAggregateIds.Keys.ToArray()[Ran(new Random(), LoadTestAggregateIds.Count - 1)];
				var version = LoadTestAggregateIds[id];
				LoadTestAggregateIds[id] = version + 1;
				var eventToStore = new EventToStore() { AggregateRootId = id, Version = version, Timestamp = DateTime.Now, EventType = typeof(byte[]).AssemblyQualifiedName, Data = new Byte[new Random().Next(99)] };
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

			var configure = Configure.With()
				.DebugLogger()
				.PartitionedFileEventStoreProvider(2, Path.Combine(BaseDirectory, Guid.NewGuid().ToString()), 5000, 8 * 1024)
				.XmlObjectSerializer(serializer)
				.EventStore()
				.MessageReceiver()
				.LRUAggregateRootCache()
				.Register<MockCommand, MockAggregateRoot>()
				.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

			//var serializer = new DataContractSerializer(typeof(object), new Type[] { typeof(MockCommand), typeof(MockEvent), typeof(MockCommand2) });
			var random = new Random();

			var keys = LoadTestAggregateIds.Keys.ToArray();
			var id = LoadTestAggregateIds.Keys.ToArray()[Ran(random, LoadTestAggregateIds.Count - 1)];
			var messageReceiver = (configure as Configure).MessageReceiver;
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = random.Next(10) - 5 });

			var stopWatch = Stopwatch.StartNew();

			var amount = 1;
			foreach (var i in Enumerable.Range(1, amount))
			{
				id = keys[Ran(random, LoadTestAggregateIds.Count - 1)];
				messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = random.Next(10) - 5 });
			}

			stopWatch.Stop();

			var fileInfos = Directory.GetFiles(((configure as Configure).EventStoreProvider as PartitionedFileEventStoreProvider).Directory, "*.*", SearchOption.AllDirectories).Select(f => new FileInfo(f)).ToList();
			Debug.WriteLine("Time taken {0}", stopWatch.Elapsed);
			Debug.WriteLine("Per sec {0:#,##0.0}", amount / stopWatch.Elapsed.TotalSeconds);
			Debug.WriteLine("Files in Event Store {0}", fileInfos.Count());
			Debug.WriteLine("Size of Event Store {0:#,##0.0} KB", fileInfos.Sum(f => f.Length / 1024.0));
			Debug.WriteLine("Avg Size of Event Store {0:#,##0.0} KB", fileInfos.Average(f => f.Length / 1024.0));
			Debug.WriteLine("Largest Size of Event Store {0:#,##0.0} KB", fileInfos.Max(f => f.Length / 1024.0));
		}

		[TestMethod]
		public void LoadTest_MessageReceiver_FileStore_Parallel()
		{
			var typeModel = RuntimeTypeModel.Create();
			typeModel.Add(typeof(MockCommand), true);
			var serializer = new XmlProtoSerializer(typeModel, typeof(MockCommand));

			var configure = Configure.With()
				.DebugLogger()
				.PartitionedFileEventStoreProvider(8, Path.Combine(BaseDirectory, Guid.NewGuid().ToString()), 1500, 8 * 1024)
				.XmlObjectSerializer(serializer)
				.EventStore()
				.MessageReceiver()
				.LRUAggregateRootCache()
				.Register<MockCommand, MockAggregateRoot>()
				.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

			var random = new Random();

			var keys = LoadTestAggregateIds.Keys.ToArray();
			var id = LoadTestAggregateIds.Keys.ToArray()[Ran(random, LoadTestAggregateIds.Count - 1)];
			var messageReceiver = (configure as Configure).MessageReceiver;
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = random.Next(10) - 5 });
			var concurrencyExceptions = 0;

			var stopWatch = Stopwatch.StartNew();

			var amount = 1;
			Parallel.ForEach(Enumerable.Range(1, amount), new ParallelOptions() { MaxDegreeOfParallelism = 8 }, i =>
			{
				id = keys[Ran(random, LoadTestAggregateIds.Count - 1)];
				var cmd = new MockCommand() { AggregateRootId = id, Increment = random.Next(10) - 5 };
					try
					{
						messageReceiver.Receive(cmd);
					}
					catch (ConcurrencyException)
					{
						try
						{
							messageReceiver.Receive(cmd);
						}
						catch (ConcurrencyException)
						{
							try
							{
								messageReceiver.Receive(cmd);
							}
							catch (ConcurrencyException)
							{
								concurrencyExceptions++;
							}
						}
					}
			});
				
			stopWatch.Stop();

			var fileInfos = Directory.GetFiles(((configure as Configure).EventStoreProvider as PartitionedFileEventStoreProvider).Directory, "*.*", SearchOption.AllDirectories).Select(f => new FileInfo(f)).ToList();
			Debug.WriteLine("Time taken {0}", stopWatch.Elapsed);
			Debug.WriteLine("Per sec {0:#,##0.0}", amount / stopWatch.Elapsed.TotalSeconds);
			Debug.WriteLine("Files in Event Store {0}", fileInfos.Count());
			Debug.WriteLine("Size of Event Store {0:#,##0.0} KB", fileInfos.Sum(f => f.Length / 1024.0));
			Debug.WriteLine("Avg Size of Event Store {0:#,##0.0} KB", fileInfos.Average(f => f.Length / 1024.0));
			Debug.WriteLine("Largest Size of Event Store {0:#,##0.0} KB", fileInfos.Max(f => f.Length / 1024.0));
		}

		[TestMethod]
		public void LoadTest_EventPublisher()
		{
			var amount = 10; // min 10

			var typeModel = RuntimeTypeModel.Create();
			typeModel.Add(typeof(MockCommand), true);
			var serializer = new XmlProtoSerializer(typeModel, typeof(MockCommand));

			var configure = Configure.With()
				.DebugLogger(false)
				//.PartitionedFileEventStoreProvider(8, Path.Combine(BaseDirectory, Guid.NewGuid().ToString()), 1500, 8 * 1024)
				.MemoryEventStoreProvider()
				//.SqlServerEventStoreProvider(ConnectionString)
				.XmlObjectSerializer(serializer)
				.EventStore()
				.MessageReceiver()
				.LRUAggregateRootCache()
				.Register<MockCommand, MockAggregateRoot>()
				.Register<MockCommand2, MockAggregateRoot>("Id", "Apply")
				.MockEventPublisher(10000, TimeSpan.FromSeconds(1))
				.Subscribe<MockSubscriber>(Guid.NewGuid());

			var publisher = (configure as Configure).EventPublisher as MockEventPublisher;
			Assert.AreEqual(1, publisher.Subscribers.Count);
			var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;

			var random = new Random();

			var keys = LoadTestAggregateIds.Keys.ToArray();
			var id = LoadTestAggregateIds.Keys.ToArray()[Ran(random, LoadTestAggregateIds.Count - 1)];
			var messageReceiver = (configure as Configure).MessageReceiver;
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = random.Next(10) - 5 });
			var concurrencyExceptions = 0;

			var stopWatch = Stopwatch.StartNew();

			subscriber.SignalOnCount = (int)((double)amount * 0.9);
			Parallel.ForEach(Enumerable.Range(1, amount), new ParallelOptions() { MaxDegreeOfParallelism = 4 }, i =>
			{
				id = keys[Ran(random, LoadTestAggregateIds.Count - 1)];
				var cmd = new MockCommand() { AggregateRootId = id, Increment = random.Next(10) - 5 };
				try
				{
					messageReceiver.Receive(cmd);
				}
				catch (ConcurrencyException)
				{
					//System.Diagnostics.Debugger.Break();
					concurrencyExceptions++;
				}
			});
			subscriber.ReceivedEvent.WaitOne(-1);

			stopWatch.Stop();

			configure.Dispose();

			//var fileInfos = Directory.GetFiles(((configure as Configure).EventStoreProvider as PartitionedFileEventStoreProvider).Directory, "*.*", SearchOption.AllDirectories).Select(f => new FileInfo(f)).ToList();
			Debug.WriteLine("Time taken {0}", stopWatch.Elapsed);
			Debug.WriteLine("Per sec {0:#,##0.0}", amount / stopWatch.Elapsed.TotalSeconds);
			//Debug.WriteLine("Files in Event Store {0}", fileInfos.Count());
			//Debug.WriteLine("Size of Event Store {0:#,##0.0} KB", fileInfos.Sum(f => f.Length / 1024.0));
			//Debug.WriteLine("Avg Size of Event Store {0:#,##0.0} KB", fileInfos.Average(f => f.Length / 1024.0));
			//Debug.WriteLine("Largest Size of Event Store {0:#,##0.0} KB", fileInfos.Max(f => f.Length / 1024.0));
		}

		private int Ran(Random random, int p)
		{
			var c = random.NextDouble();

			if (0.5 > c)
			{
				return random.Next(p / 100);
			}

			if (0.75 > c)
			{
				return random.Next(p / 10);
			}

			if (0.95 > c)
			{
				return random.Next(p / 2);
			}

			return random.Next(p);
		}
	}
}
