using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Yeast.EventStore
{
	[Serializable]
	public class ConcurrencyException : EventToStoreException, ISerializable
	{
		public ConcurrencyException() : base() { }
		public ConcurrencyException(string message) : base(message) { }
		public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
		public ConcurrencyException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public Guid AggregateId { get; set; }
		public int Version { get; set; }
	}
}
