using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IEventReceiver
	{
		void Receive<T>(T @event);
	}
}
