using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// A simple way to partition events among a number of persisters.
	/// </summary>
	public interface IPartitionedEventPersister : IEventPersister
	{
		/// <summary>
		/// The number of seperate persisters to use.
		/// </summary>
		int MaximumPartitions { get; }
	}
}
