using System;

namespace DomainCQRS
{
	/// <summary>
	/// Holds the Aggregate Root for the cache.  Helps to keep track of the latest version as well.
	/// </summary>
	public class AggregateRootAndVersion : IEquatable<AggregateRootAndVersion>, IEquatable<Guid>
	{
		/// <summary>
		/// The Id of the Aggregate Root
		/// </summary>
		public Guid AggregateRootId;
		/// <summary>
		/// The latest version of the Aggregate Root being tracked.
		/// </summary>
		public int LatestVersion;
		/// <summary>
		/// The instance of the Aggregate Root
		/// </summary>
		public object AggregateRoot;

		/// <summary>
		/// Compares to another <see cref="AggregateRootAndVersion"/> using the <see cref="AggregateRootId"/>
		/// </summary>
		/// <param name="other">The other to compare to.</param>
		/// <returns>If the other is equl.</returns>
		public bool Equals(AggregateRootAndVersion other)
		{
			if (null == other)
			{
				throw new ArgumentNullException("other");
			}

			return AggregateRootId.Equals(other.AggregateRootId);
		}

		/// <summary>
		/// Compares to another <see cref="Guid"/> using the <see cref="AggregateRootId"/>
		/// </summary>
		/// <param name="other">The other Id to compare to.</param>
		/// <returns>If the other is equl.</returns>
		public bool Equals(Guid other)
		{
			return AggregateRootId.Equals(other);
		}

		/// <summary>
		/// Compares to another <see cref="object"/> using the <see cref="AggregateRootId"/>.  If the other is null then the comparison fails.
		/// </summary>
		/// <param name="other">The other Id to compare to.</param>
		/// <returns>If the other is equl.</returns>
		public override bool Equals(object obj)
		{
			var o = obj as AggregateRootAndVersion;
			if (null == o)
			{
				return false;
			}
			return AggregateRootId == o.AggregateRootId;
		}

		/// <summary>
		/// Uses the AggregateRootId for the hashcode
		/// </summary>
		/// <returns>The hashcode</returns>
		public override int GetHashCode()
		{
			return AggregateRootId.GetHashCode();
		}
	}
}
