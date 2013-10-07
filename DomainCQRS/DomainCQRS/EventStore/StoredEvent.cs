using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	public class StoredEvent : EventArgs
	{
		public Guid AggregateRootId { get; set; }
		public int Version { get; set; }
		public string AggregateRootType { get; set; }
		public DateTime Timestamp { get; set; }
		public object Event { get; set; }
	}
}
