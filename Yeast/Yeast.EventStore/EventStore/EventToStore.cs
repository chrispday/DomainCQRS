using System;
using System.Collections.Generic;

namespace Yeast.EventStore
{
	public class EventToStore
	{
		public Guid AggregateRootId { get; set; }
		public int Version { get; set; }
		public DateTime Timestamp { get; set; }
		public byte[] Data { get; set; }
	}
}
