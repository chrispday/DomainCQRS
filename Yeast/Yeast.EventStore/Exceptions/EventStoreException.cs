using System;
using System.Collections.Generic;

using System.Runtime.Serialization;
using System.Text;

namespace Yeast.EventStore
{
	[Serializable]
	public class EventStoreException : Exception, ISerializable
	{
		public EventStoreException() : base() { }
		public EventStoreException(string message) : base(message) { }
		public EventStoreException(string message, Exception innerException) : base(message, innerException) { }
		public EventStoreException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}
