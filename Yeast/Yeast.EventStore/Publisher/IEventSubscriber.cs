﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IEventSubscriber
	{
		void Receive(object @event);
	}
}
