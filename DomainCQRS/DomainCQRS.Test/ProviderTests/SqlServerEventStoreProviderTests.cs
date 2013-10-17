using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using DomainCQRS.Persister;
using DomainCQRS.Common;

namespace DomainCQRS.Test
{
	[TestClass]
	public class SqlServerEventStoreProviderTests : EventStoreProviderTestsBase
	{
		string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=EventStore;Integrated Security=True";

		protected override IEventPersister CreateProvider()
		{
			return new SqlServerEventPersister(new DebugLogger(true), ConnectionString);
		}

		protected override bool ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder
		{
			get { return false; }
		}

		[TestInitialize]
		public void Init()
		{
			try
			{
				using (var conn = new SqlConnection(ConnectionString))
				{
					conn.Open();
					new SqlCommand("drop table [Event]", conn).ExecuteNonQuery();
				}
			}
			catch { }
			try
			{
				using (var conn = new SqlConnection(ConnectionString))
				{
					conn.Open();
					new SqlCommand("drop table [Subscriber]", conn).ExecuteNonQuery();
				}
			}
			catch { }
		}

		[TestCleanup]
		public void Cleanup()
		{
		}

		[TestMethod]
		public void SqlServerEventStoreProvider_EnsuresExists()
		{
			var sqlEventEventStoreProvider = new SqlServerEventPersister(new DebugLogger(true), ConnectionString).EnsureExists();
			using (var conn = new SqlConnection(ConnectionString))
			{
				conn.Open();
				using (var reader = new SqlCommand("select top 1 * from [Event]", conn).ExecuteReader())
				{
					while (reader.Read()) { }
				}
			}
		}

		protected override IConfigure RegisterProvider(IConfigure configure)
		{
			return configure.SqlServerEventPersister(ConnectionString);
		}
	}
}
