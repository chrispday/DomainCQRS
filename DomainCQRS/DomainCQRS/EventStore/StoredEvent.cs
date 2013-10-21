using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Meta-data of an event thats been loaded from the provider.
	/// </summary>
	public class StoredEvent : EventArgs
	{
		public Guid AggregateRootId { get; set; }
		public int Version { get; set; }
		public string AggregateRootType { get; set; }
		public DateTime Timestamp { get; set; }
		public object Event { get; set; }
	}
}
