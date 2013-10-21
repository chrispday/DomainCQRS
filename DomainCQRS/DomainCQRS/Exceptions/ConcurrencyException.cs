using System;
using System.Collections.Generic;

using System.Runtime.Serialization;
using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Is thrown when an event is saved for a version of an Aggregate Root that already exists.
	/// </summary>
	[Serializable]
	public class ConcurrencyException : EventToStoreException, ISerializable
	{
		public ConcurrencyException() : base() { }
		public ConcurrencyException(string message) : base(message) { }
		public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
		public ConcurrencyException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public Guid AggregateRootId { get; set; }
		public int Version { get; set; }
	}
}
