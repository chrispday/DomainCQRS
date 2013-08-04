using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Domain
{
	public interface ICommand
	{
		Guid AggregateRootId { get; set; }
	}
}
