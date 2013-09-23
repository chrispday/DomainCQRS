using System;
using System.Collections;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Domain
{
	public interface IHandlesCommand<C>
		where C : ICommand
	{
		IEnumerable<IEvent> Apply(C command);
	}

	public interface IHandlesCommand<C, E>
		where C : ICommand
		where E : IEvent
	{
		E Apply(C command);
	}
}
