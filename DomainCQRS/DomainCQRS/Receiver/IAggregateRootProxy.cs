using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	public interface IAggregateRootProxy
	{
		Type Type { get; }

		object Create();
		IEnumerable ApplyCommand(object aggregateRoot, object command);
		void ApplyEvent(object aggregateRoot, object @event);

		IAggregateRootProxy Register(IMessageProxy messageProxy, string aggregateRootApplyMethod);
	}
}
