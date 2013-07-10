using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Yeast.EventStore
{
	[Serializable]
	public class CommandApplyException : EventStoreException, ISerializable
	{
		public CommandApplyException() : base() { }
		public CommandApplyException(string message) : base(message) { }
		public CommandApplyException(string message, Exception innerException) : base(message, innerException) { }
		public CommandApplyException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public Type AggregateType { get; set; }
		public Type CommandType { get; set; }
	}
}
