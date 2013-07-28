using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IEventSubscriber
	{
		ILogger Logger { get; set; }
		IEventSubscriber Receive(object @event);
	}
}
