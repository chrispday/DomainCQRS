using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	public class StoredEvent
	{
		public Guid AggregateRootId { get; set; }
		public int Version { get; set; }
		public string AggregateRootType { get; set; }
		public object Event { get; set; }
	}
}
