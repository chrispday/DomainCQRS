using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using DomainCQRS.Common;
using DomainCQRS.Persister;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class SqlServerEventPersisterConfigure
	{
		/// <summary>
		/// Configures Domain CQRS to use SQL Server persistance.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/></param>
		/// <param name="connectionString">The SQL Server connection string</param>
		/// <returns>The <see cref="IConfigure"/></returns>
		public static IConfigure SqlServerEventPersister(this IConfigure configure, string connectionString)
		{
			if (string.IsNullOrEmpty(connectionString))
			{
				throw new ArgumentNullException("connectionString");
			}

			configure.Registry
				.BuildInstancesOf<IEventPersister>()
				.TheDefaultIs(Registry.Instance<IEventPersister>()
					.UsingConcreteType<SqlServerEventPersister>()
					.WithProperty("connectionString").EqualTo(connectionString))
				.AsSingletons();
			return configure;
		}
	}
}

namespace DomainCQRS.Persister
{
	/// <summary>
	/// Persists events to SQL Server
	/// </summary>
	public class SqlServerEventPersister : IEventPersister
	{
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		private readonly string _connectionString;
		public string ConnectionString { get { return _connectionString; } }

		public SqlServerEventPersister(ILogger logger, string connectionString)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == connectionString)
			{
				throw new ArgumentNullException("connectionString");
			}

			_logger = logger;
			_connectionString = connectionString;
		}

		#region Sql Commands

		private static string InsertEvent = @"
insert into [Event] ([AggregateRootId], [Version], [Timestamp], [AggregateRootType], [EventType], [Data]) values (@AggregateRootId, @Version, @Timestamp, @AggregateRootType, @EventType, @Data)
";
		private static string SelectEvents = @"
select [Version], [Timestamp], [AggregateRootType], [EventType], [Data] 
from [Event] with (NOLOCK) 
where [AggregateRootId] = @AggregateRootId and [Version] >= @FromVersion and [Version] <= @ToVersion and [Timestamp] >= @FromTimestamp and [Timestamp] <= @ToTimestamp order by Version
";
		private static string SelectEventsBySequence = @"
select [Version], [Timestamp], [AggregateRootType], [EventType], [Data], [AggregateRootId], [Sequence]
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
	[AggregateRootType] [varchar](max) NOT NULL,
	[EventType] [varchar](max) NOT NULL,
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

		public IEventPersister EnsureExists()
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

		public IEventPersister Save(EventToStore eventToStore)
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
				var timeStamp = eventToStore.Timestamp;
				if (timeStamp < SqlDateTime.MinValue.Value)
				{
					timeStamp = SqlDateTime.MinValue.Value;
				}
				if (timeStamp > SqlDateTime.MaxValue.Value)
				{
					timeStamp = SqlDateTime.MaxValue.Value;
				}
				cmd.Parameters.Add(new SqlParameter("@AggregateRootId", eventToStore.AggregateRootId));
				cmd.Parameters.Add(new SqlParameter("@Version", eventToStore.Version));
				cmd.Parameters.Add(new SqlParameter("@Timestamp", timeStamp));
				cmd.Parameters.Add(new SqlParameter("@AggregateRootType", eventToStore.AggregateRootType));
				cmd.Parameters.Add(new SqlParameter("@EventType", eventToStore.EventType));
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
			using (var cmd = new SqlCommand() { Connection = conn, CommandText = SelectEvents })
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
						yield return CreateEventToStore(reader, aggregateRootId);
					}
				}
			}
		}


		public IEventPersisterPosition CreatePosition()
		{
			return new SqlServerEventPersisterPosition();
		}

		public IEventPersisterPosition LoadPosition(Guid subscriberId)
		{
			using (var conn = new SqlConnection(ConnectionString))
			using (var cmd = new SqlCommand() { Connection = conn, CommandText = SelectPosition })
			{
				cmd.Parameters.Add(new SqlParameter("@SubscriberId", subscriberId));
				conn.Open();
				var position = cmd.ExecuteScalar();
				return new SqlServerEventPersisterPosition() { Position = (long) (position ?? 0L) };
			}
		}

		public IEventPersister SavePosition(Guid subscriberId, IEventPersisterPosition position)
		{
			return SaveEventStoreProviderPosition(subscriberId, position as SqlServerEventPersisterPosition);
		}

		public IEventPersister SaveEventStoreProviderPosition(Guid subscriberId, SqlServerEventPersisterPosition position)
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

		public IEnumerable<EventToStore> Load(IEventPersisterPosition from, IEventPersisterPosition to)
		{
			return Load(from as SqlServerEventPersisterPosition, to as SqlServerEventPersisterPosition);
		}

		public IEnumerable<EventToStore> Load(SqlServerEventPersisterPosition from, SqlServerEventPersisterPosition to)
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
						to.Position = reader.GetInt64(6);
						yield return CreateEventToStore(reader, reader.GetGuid(5));
					}
				}
			}
		}

		private EventToStore CreateEventToStore(SqlDataReader reader, Guid aggregateRootId)
		{
			return new EventToStore()
			{
				AggregateRootId = aggregateRootId,
				Version = reader.GetInt32(0),
				Timestamp = reader.GetDateTime(1),
				AggregateRootType = reader.GetString(2),
				EventType = reader.GetString(3),
				Data = reader.GetSqlBinary(4).Value
			};
		}

		public void Dispose()
		{
		}
	}
}
