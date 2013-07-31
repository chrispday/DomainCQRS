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
		public MockSubscriber()
		{
		}

		public Common.ILogger Logger { get;  set; }

		public AutoResetEvent ReceivedEvent = new AutoResetEvent(false);
		public volatile static int SignalOnCount = 1;
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
