using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Yeast.EventStore
{
	[Serializable]
	public class CommandHandlerException : EventStoreException, ISerializable
	{
		public CommandHandlerException() : base() { }
		public CommandHandlerException(string message) : base(message) { }
		public CommandHandlerException(string message, Exception innerException) : base(message, innerException) { }
		public CommandHandlerException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public Type AggregateType { get; set; }
		public Type CommandType { get; set; }
	}
}
