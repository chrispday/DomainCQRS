using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IHandles<M>
	{
		IEnumerable Apply(M message);
	}
}
