using System;
using System.Collections.Generic;

using System.Runtime.Serialization;
using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Thrown when an error occurs during registration.
	/// </summary>
	[Serializable]
	public class RegistrationException : EventStoreException, ISerializable
	{
		public RegistrationException() : base() { }
		public RegistrationException(string message) : base(message) { }
		public RegistrationException(string message, Exception innerException) : base(message, innerException) { }
		public RegistrationException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public Type MessageType { get; set; }
	}
}
