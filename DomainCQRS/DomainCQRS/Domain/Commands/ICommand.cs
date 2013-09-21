using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Domain
{
	public interface ICommand
	{
		Guid AggregateRootId { get; set; }
	}
}
