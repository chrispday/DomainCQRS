using System;
using System.Collections.Generic;

namespace DomainCQRS
{
	public class EventToStore
	{
		public Guid AggregateRootId { get; set; }
		public string AggregateRootType { get; set; }
		public int Version { get; set; }
		public DateTime Timestamp { get; set; }
		public string EventType { get; set; }
		public byte[] Data { get; set; }
		public int Size { get; set; }
	}
}
