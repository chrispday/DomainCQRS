﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IHandles<E>
	{
		E When(E @event);
	}
}
