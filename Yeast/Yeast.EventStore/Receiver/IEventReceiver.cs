using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IEventReceiver
	{
		IEventStore EventStore { get; set; }

		void Receive(object command);
		IEventReceiver Register<AR, C>()
			where AR : IAggregateRoot
			where C : ICommand;
	}
}
