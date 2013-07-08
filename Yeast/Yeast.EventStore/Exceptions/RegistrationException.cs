using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Yeast.EventStore
{
	[Serializable]
	public class RegistrationException : EventStoreException, ISerializable
	{
		public RegistrationException() : base() { }
		public RegistrationException(string message) : base(message) { }
		public RegistrationException(string message, Exception innerException) : base(message, innerException) { }
		public RegistrationException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public Type CommandType { get; set; }
	}
}
