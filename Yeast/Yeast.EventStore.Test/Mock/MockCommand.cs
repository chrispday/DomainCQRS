using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Yeast.EventStore.Domain;
using Yeast.EventStore.Test.Mock;

namespace Yeast.EventStore
{
	[Serializable]
	[DataContract]
	[ProtoContract]
	[ProtoInclude(100, typeof(MockEvent))]
	public class MockCommand : ICommand
	{
		[DataMember]
		[ProtoMember(1)]
		public int Increment { get; set; }
		[DataMember]
		[ProtoMember(2)]
		public Guid AggregateRootId { get; set; }
	}

	[Serializable]
	[DataContract]
	[ProtoContract]
	public class MockEvent : MockCommand, IEvent
	{
		public int BatchNo;
	}

	[Serializable]
	[DataContract]
	[ProtoContract]
	public class MockEvent2 : MockEvent, IEvent
	{
		public MockEvent2() { }
		public MockEvent2(MockEvent mockEvent)
		{
			Increment = -1 * mockEvent.Increment;
			AggregateRootId = mockEvent.AggregateRootId;
			BatchNo = mockEvent.BatchNo;
		}
	}

	[Serializable]
	[DataContract]
	[ProtoContract]
	public class MockCommand2
	{
		[DataMember]
		[ProtoMember(1)]
		public int Increment { get; set; }
		[DataMember]
		[ProtoMember(2)]
		public Guid Id { get; set; }
	}
}
