using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Yeast.EventStore
{
	[Serializable]
	public class EventWhenException : EventStoreException, ISerializable
	{
		public EventWhenException() : base() { }
		public EventWhenException(string message) : base(message) { }
		public EventWhenException(string message, Exception innerException) : base(message, innerException) { }
		public EventWhenException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public Type AggregateType { get; set; }
		public Type EventType { get; set; }
	}
}
