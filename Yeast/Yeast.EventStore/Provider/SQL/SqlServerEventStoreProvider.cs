using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;
using Yeast.EventStore.Provider;

namespace Yeast.EventStore
{
	public static class SqlServerEventStoreProviderConfigure
	{
		public static IConfigure SqlServerEventStoreProvider(this IConfigure configure, string connectionString)
		{
			if (string.IsNullOrEmpty(connectionString))
			{
				throw new ArgumentNullException("connectionString");
			}

			var c = configure as Configure;
			c.EventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = connectionString, Logger = c.Logger };
			return configure;
		}
	}
}

namespace Yeast.EventStore.Provider
{
	public class SqlServerEventStoreProvider : IEventStoreProvider
	{
		public ILogger Logger { get; set; }
		public string ConnectionString { get; set; }

		#region Sql Commands

		private static string InsertCommand = @"
insert into [Event] ([AggregateRootId], [Version], [Timestamp], [Data]) values (@AggregateRootId, @Version, @Timestamp, @Data)
";
		private static string SelectCommand = @"
select [Version], [Timestamp], [Data] 
from [Event] with (NOLOCK) 
where [AggregateRootId] = @AggregateRootId and [Version] >= @FromVersion and [Version] <= @ToVersion and [Timestamp] >= @FromTimestamp and [Timestamp] <= @ToTimestamp order by Version
";
		private static string SelectCommandWithSequence = @"
select [Sequence], [AggregateRootId], [Version], [Timestamp], [Data] 
from [Event] with (NOLOCK)
where [Sequence] > @FromSequence
order by [Sequence]";

		private static string DropDefault = @"
IF  EXISTS (SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'[DF_Event_Timestamp]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Event] DROP CONSTRAINT [DF_Event_Timestamp]
END";
		private static string DropTable = @"
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Event]') AND type in (N'U'))
DROP TABLE [dbo].[Event]";
		private static string CreateTableCommand = @"
-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'Event'
CREATE TABLE [Event] (
	[AggregateRootId] uniqueidentifier  NOT NULL,
	[Version] int  NOT NULL,
	[Timestamp] datetime NOT NULL,
	[Sequence] int IDENTITY(1, 1) NOT NULL,
	[Data] varbinary(max)  NOT NULL
);";

		private static string CreateTableIndexesCommand = @"
-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [AggregateRootId], [Version] in table 'Event'
ALTER TABLE [Event]
ADD CONSTRAINT [PK_Events]
    PRIMARY KEY CLUSTERED ([AggregateRootId], [Version] ASC);

CREATE NONCLUSTERED INDEX NCI_Sequence ON [Event] ([Sequence]);
";

		private static string CreateTableDefaultCommand = @"
ALTER TABLE [dbo].[Event] ADD  CONSTRAINT [DF_Event_Timestamp]  DEFAULT (getdate()) FOR [Timestamp];
";

		#endregion

		public IEventStoreProvider EnsureExists()
		{
			try
			{
				Load(Guid.Empty, null, null, null, null).FirstOrDefault();
				Load(new SqlServerEventStoreProviderPosition() { Position = Int32.MaxValue }, new SqlServerEventStoreProviderPosition()).FirstOrDefault();
			}
			catch (SqlException)
			{
				using (var conn = new SqlConnection(ConnectionString))
				{
					conn.Open();
					using (var cmd = new SqlCommand() { Connection = conn, CommandText = DropDefault })
					{
						cmd.ExecuteNonQuery();
					}
					using (var cmd = new SqlCommand() { Connection = conn, CommandText = DropTable })
					{
						cmd.ExecuteNonQuery();
					}
					using (var cmd = new SqlCommand() { Connection = conn, CommandText = CreateTableCommand })
					{
						cmd.ExecuteNonQuery();
					}
					using (var pkCmd = new SqlCommand() { Connection = conn, CommandText = CreateTableIndexesCommand })
					{
						pkCmd.ExecuteNonQuery();
					}
					using (var dCmd = new SqlCommand() { Connection = conn, CommandText = CreateTableDefaultCommand })
					{
						dCmd.ExecuteNonQuery();
					}
				}
			}

			return this;
		}

		public IEventStoreProvider Save(EventToStore eventToStore)
		{
			if (0 > eventToStore.Version)
			{
				throw new EventToStoreException("Version must be 0 or greater.") { EventToStore = eventToStore };
			}
			if (null == eventToStore.Data)
			{
				throw new EventToStoreException("Data cannot be null.") { EventToStore = eventToStore };
			}

			using (var conn = new SqlConnection(ConnectionString))
			using (var cmd = new SqlCommand() { Connection = conn, CommandText = InsertCommand })
			{
				cmd.Parameters.Add(new SqlParameter("@AggregateRootId", eventToStore.AggregateRootId));
				cmd.Parameters.Add(new SqlParameter("@Version", eventToStore.Version));
				cmd.Parameters.Add(new SqlParameter("@Timestamp", eventToStore.Timestamp));
				cmd.Parameters.Add(new SqlParameter("@Data", eventToStore.Data));
				conn.Open();
				try
				{
					cmd.ExecuteNonQuery();
				}
				catch (SqlException sEx)
				{
					if (sEx.Errors.Cast<SqlError>().Any(sqlError => 2627 == sqlError.Number))
					{
						throw new ConcurrencyException("Version already exists.", sEx) { EventToStore = eventToStore, AggregateRootId = eventToStore.AggregateRootId, Version = eventToStore.Version };
					}

					throw;
				}
			}

			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			using (var conn = new SqlConnection(ConnectionString))
			using (var cmd = new SqlCommand() { Connection = conn, CommandText =  SelectCommand })
			{
				cmd.Parameters.Add(new SqlParameter("@AggregateRootId", aggregateRootId));
				cmd.Parameters.Add(new SqlParameter("@FromVersion", fromVersion.GetValueOrDefault(-1)));
				cmd.Parameters.Add(new SqlParameter("@ToVersion", toVersion.GetValueOrDefault(System.Data.SqlTypes.SqlInt32.MaxValue.Value)));
				cmd.Parameters.Add(new SqlParameter("@FromTimestamp", fromTimestamp.GetValueOrDefault(System.Data.SqlTypes.SqlDateTime.MinValue.Value)));
				cmd.Parameters.Add(new SqlParameter("@ToTimestamp", toTimestamp.GetValueOrDefault(System.Data.SqlTypes.SqlDateTime.MaxValue.Value)));
				conn.Open();
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						yield return new EventToStore()
						{
							AggregateRootId = aggregateRootId,
							Version = reader.GetInt32(0),
							Timestamp = reader.GetDateTime(1),
							Data = reader.GetSqlBinary(2).Value
						};
					}
				}
			}
		}


		public IEventStoreProviderPosition CreateEventStoreProviderPosition()
		{
			return new SqlServerEventStoreProviderPosition();
		}

		public IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			return Load(from as SqlServerEventStoreProviderPosition, to as SqlServerEventStoreProviderPosition);
		}

		public IEnumerable<EventToStore> Load(SqlServerEventStoreProviderPosition from, SqlServerEventStoreProviderPosition to)
		{
			Logger.Verbose("from {0} to {1}", from, to);

			to.Position = from.Position;

			using (var conn = new SqlConnection(ConnectionString))
			using (var cmd = new SqlCommand() { Connection = conn, CommandText = SelectCommandWithSequence })
			{
				cmd.Parameters.Add(new SqlParameter("@FromSequence", from.Position));
				conn.Open();
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						to.Position = reader.GetInt32(0);
						yield return new EventToStore()
						{
							AggregateRootId = reader.GetGuid(1),
							Version = reader.GetInt32(2),
							Timestamp = reader.GetDateTime(3),
							Data = reader.GetSqlBinary(4).Value
						};
					}
				}
			}
		}

		public void Dispose()
		{
		}
	}
}
