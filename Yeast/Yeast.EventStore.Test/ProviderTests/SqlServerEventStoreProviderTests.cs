﻿using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using Yeast.EventStore.Provider;
using Yeast.EventStore.Common;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class SqlServerEventStoreProviderTests : EventStoreProviderTestsBase
	{
		string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=EventStore;Integrated Security=True";

		protected override IEventStoreProvider CreateProvider()
		{
			return new SqlServerEventStoreProvider() { ConnectionString = ConnectionString, Logger = new DebugLogger() };
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
	}
}
