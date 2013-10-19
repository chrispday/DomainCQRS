using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DomainCQRS.Common;

namespace DomainCQRS.Test.Mock
{
	public class MockSubscriber : IEventProjector<object>
	{
		public MockSubscriber()
		{
		}

		public ILogger Logger { get;  set; }

		public AutoResetEvent ReceivedEvent = new AutoResetEvent(false);
		public volatile int SignalOnCount = 1;
		public List<object> Received = new List<object>();
		public void Receive(object @event)
		{
			Received.Add(@event);
			if (SignalOnCount <= Received.Count)
			{
				ReceivedEvent.Set();
			}
		}

		private Guid _subscriptionId = Guid.NewGuid();
		public Guid SubscriptionId
		{
			get { return _subscriptionId; }
		}
	}
}
