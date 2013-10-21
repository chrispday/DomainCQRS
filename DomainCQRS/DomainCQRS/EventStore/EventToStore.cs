using System;
using System.Collections.Generic;

namespace DomainCQRS
{
	/// <summary>
	/// Meta-data required to store a serialised event.
	/// </summary>
	public class EventToStore
	{
		public Guid AggregateRootId { get; set; }
		public string AggregateRootType { get; set; }
		public int Version { get; set; }
		public DateTime Timestamp { get; set; }
		public string EventType { get; set; }
		public byte[] Data { get; set; }
	}
}
