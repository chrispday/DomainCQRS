using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Domain
{
	public interface IHandlesCommand<C>
		where C : ICommand
	{
		IEnumerable Apply(C command);
	}
}
