using System;
using System.Collections;
using System.Collections.Generic;

using System.Text;

namespace Yeast.EventStore.Domain
{
	public interface IHandlesCommand<C>
	{
		IEnumerable Apply(C command);
	}
}
