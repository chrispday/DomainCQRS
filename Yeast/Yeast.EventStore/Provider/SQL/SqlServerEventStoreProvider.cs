using System;
using System.Collections.Generic;
using System.Data.SqlClient;

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
			c.EventStoreProvider = new SqlServerEventStoreProvider() { ConnectionString = connectionString, Logger = c.Logger }.EnsureExists();
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

		private static string InsertEvent = @"
insert into [Event] ([AggregateRootId], [Version], [Timestamp], [Data]) values (@AggregateRootId, @Version, @Timestamp, @Data)
";
		private static string SelectEvents = @"
select [Version], [Timestamp], [Data] 
from [Event] with (NOLOCK) 
where [AggregateRootId] = @AggregateRootId and [Version] >= @FromVersion and [Version] <= @ToVersion and [Timestamp] >= @FromTimestamp and [Timestamp] <= @ToTimestamp order by Version
";
		private static string SelectEventsBySequence = @"
select [Sequence], [AggregateRootId], [Version], [Timestamp], [Data] 
from [Event] with (NOLOCK)
where [Sequence] > @FromSequence
order by [Sequence]";

		private static string InsertUpdatePosition = @"
if exists (select 1 from [Subscriber] where [SubscriberId] = @SubscriberId)
	update [Subscriber] set [Position] = @Position where [SubscriberId] = @SubscriberId
else
	insert into [Subscriber] ([SubscriberId], [Position]) values (@SubscriberId, @Position)";

		private static string SelectPosition = @"
select [Position] from [Subscriber] where [SubscriberId] = @SubscriberId";

		private static string[] CreateEventCommands = new string[] {
/*@"IF  EXISTS (SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'[DF_Event_Timestamp]') AND type = 'D')
	ALTER TABLE [dbo].[Event] DROP CONSTRAINT [DF_Event_Timestamp]",
@"IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Event]') AND type in (N'U'))
	DROP TABLE [dbo].[Event]",*/
@"CREATE TABLE [Event] (
	[AggregateRootId] [uniqueidentifier]  NOT NULL,
	[Version] [int]  NOT NULL,
	[Timestamp] [datetime] NOT NULL,
	[Sequence] [bigint] IDENTITY(1, 1) NOT NULL,
	[Data] [varbinary](max)  NOT NULL);",
@"ALTER TABLE [Event] ADD CONSTRAINT [PK_Events] PRIMARY KEY CLUSTERED ([AggregateRootId], [Version] ASC);
CREATE NONCLUSTERED INDEX NCI_Sequence ON [Event] ([Sequence]);",
@"ALTER TABLE [dbo].[Event] ADD  CONSTRAINT [DF_Event_Timestamp]  DEFAULT (getdate()) FOR [Timestamp];" };
		private static string[] CreateSubscriberCommands = new string[] {
/*@"IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Subscriber]') AND type in (N'U'))
	DROP TABLE [dbo].[Subscriber]",*/
@"CREATE TABLE [dbo].[Subscriber](
	[SubscriberId] [uniqueidentifier] NOT NULL,
	[Position] [bigint] NOT NULL,
 CONSTRAINT [PK_Subscriber] PRIMARY KEY CLUSTERED ([SubscriberId] ASC) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]"
		};

		#endregion

		public IEventStoreProvider EnsureExists()
		{
			try
			{
				using (var enumerator = Load(Guid.Empty, null, null, null, null).GetEnumerator())
				{
					enumerator.MoveNext();
				}
			}
			catch (SqlException)
			{
				using (var conn = new SqlConnection(ConnectionString))
				{
					conn.Open();
					foreach (var sql in CreateEventCommands)
					{
						using (var cmd = new SqlCommand(sql, conn))
						{
							cmd.ExecuteNonQuery();
						}
					}
				}
			}
			try
			{
				LoadPosition(Guid.Empty);
			}
			catch (SqlException)
			{
				using (var conn = new SqlConnection(ConnectionString))
				{
					conn.Open();
					foreach (var sql in CreateSubscriberCommands)
					{
						using (var cmd = new SqlCommand(sql, conn))
						{
							cmd.ExecuteNonQuery();
						}
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
			using (var cmd = new SqlCommand() { Connection = conn, CommandText = InsertEvent })
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
					foreach (SqlError sqlError in sEx.Errors)
					{
						if (2627 == sqlError.Number)
						{
							throw new ConcurrencyException("Version already exists.", sEx) { EventToStore = eventToStore, AggregateRootId = eventToStore.AggregateRootId, Version = eventToStore.Version };
						}
					}

					throw;
				}
			}

			return this;
		}

		public IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			using (var conn = new SqlConnection(ConnectionString))
			using (var cmd = new SqlCommand() { Connection = conn, CommandText =  SelectEvents })
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


		public IEventStoreProviderPosition CreatePosition()
		{
			return new SqlServerEventStoreProviderPosition();
		}

		public IEventStoreProviderPosition LoadPosition(Guid subscriberId)
		{
			using (var conn = new SqlConnection(ConnectionString))
			using (var cmd = new SqlCommand() { Connection = conn, CommandText = SelectPosition })
			{
				cmd.Parameters.Add(new SqlParameter("@SubscriberId", subscriberId));
				conn.Open();
				var position = cmd.ExecuteScalar();
				return new SqlServerEventStoreProviderPosition() { Position = (long) (position ?? 0L) };
			}
		}

		public IEventStoreProvider SavePosition(Guid subscriberId, IEventStoreProviderPosition position)
		{
			return SaveEventStoreProviderPosition(subscriberId, position as SqlServerEventStoreProviderPosition);
		}

		public IEventStoreProvider SaveEventStoreProviderPosition(Guid subscriberId, SqlServerEventStoreProviderPosition position)
		{
			using (var conn = new SqlConnection(ConnectionString))
			using (var cmd = new SqlCommand() { Connection = conn, CommandText = InsertUpdatePosition })
			{
				cmd.Parameters.Add(new SqlParameter("@SubscriberId", subscriberId));
				cmd.Parameters.Add(new SqlParameter("@Position", position.Position));
				conn.Open();
				cmd.ExecuteNonQuery();
			}
			return this;
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
			using (var cmd = new SqlCommand() { Connection = conn, CommandText = SelectEventsBySequence })
			{
				cmd.Parameters.Add(new SqlParameter("@FromSequence", from.Position));
				conn.Open();
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						to.Position = reader.GetInt64(0);
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
