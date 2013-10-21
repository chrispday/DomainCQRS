﻿using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS
{
	public interface ISagaPublisher : IEventProjector<object>
	{
		IMessageSender Sender { get; }
		ISagaPublisher Saga<Event>();
	}
}
