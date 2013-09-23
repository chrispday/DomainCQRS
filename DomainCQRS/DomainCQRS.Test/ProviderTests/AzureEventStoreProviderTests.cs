using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DomainCQRS.Common;
using Microsoft.WindowsAzure.Storage;
using DomainCQRS.Azure.Provider;
using Microsoft.WindowsAzure.Storage.Table;

namespace DomainCQRS.Test
{
	[TestClass]
	public class AzureEventStoreProviderTests : EventStoreProviderTestsBase
	{
		public string ConnectionString = "UseDevelopmentStorage=true";
		private static readonly string EventTable = "Event";
		private static readonly string SubscriberTable = "Subscriber";
		private static readonly string AggregateRootIdsTable = "AggregateRootIds";

		protected override IEventStoreProvider CreateProvider()
		{
			var _storageAccount = CloudStorageAccount.Parse(ConnectionString);
			var _tableClient = _storageAccount.CreateCloudTableClient();

			var _events = _tableClient.GetTableReference(EventTable);
			_events.DeleteIfExists();

			var _aggregateRootIds = _tableClient.GetTableReference(AggregateRootIdsTable);
			_aggregateRootIds.DeleteIfExists();

			var _subscribers = _tableClient.GetTableReference(SubscriberTable);
			_subscribers.DeleteIfExists();

			return new AzureEventStoreProvider(new DebugLogger(), ConnectionString).EnsureExists();
		}

		protected override bool ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder
		{
			get { return false; }
		}

		[TestInitialize]
		public void Init()
		{
		}

		[TestCleanup]
		public void Cleanup()
		{
		}
	}
}
