using System;
using System.Collections.Generic;

using System.Runtime.Serialization;
using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Thrown when an error occurs while storing an event.
	/// </summary>
	[Serializable]
	public class EventToStoreException : EventStoreException, ISerializable
	{
		public EventToStoreException() : base() { }
		public EventToStoreException(string message) : base(message) { }
		public EventToStoreException(string message, Exception innerException) : base(message, innerException) { }
		public EventToStoreException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public EventToStore EventToStore { get; set; }
	}
}
