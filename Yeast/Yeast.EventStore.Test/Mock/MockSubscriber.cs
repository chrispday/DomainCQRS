using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yeast.EventStore.Test.Mock
{
	public class MockSubscriber : IEventSubscriber
	{
		public static List<MockSubscriber> MockSubscribers = new List<MockSubscriber>();

		public MockSubscriber()
		{
			MockSubscribers.Add(this);
		}

		public Common.ILogger Logger { get;  set; }

		public AutoResetEvent ReceivedEvent = new AutoResetEvent(false);
		public static int SignalOnCount = 1;
		public List<object> Received = new List<object>();
		public IEventSubscriber Receive(object @event)
		{
			Received.Add(@event);
			if (SignalOnCount <= Received.Count)
			{
				ReceivedEvent.Set();
			}
			return this;
		}
	}
}
