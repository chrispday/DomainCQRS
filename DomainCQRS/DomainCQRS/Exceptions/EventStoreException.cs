﻿using System;
using System.Collections.Generic;

using System.Runtime.Serialization;
using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Thrown when an error occurs in the event store.
	/// </summary>
	[Serializable]
	public class EventStoreException : Exception, ISerializable
	{
		public EventStoreException() : base() { }
		public EventStoreException(string message) : base(message) { }
		public EventStoreException(string message, Exception innerException) : base(message, innerException) { }
		public EventStoreException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}
