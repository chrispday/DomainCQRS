﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public class AggregateRootAndVersion : IEquatable<AggregateRootAndVersion>, IEquatable<Guid>
	{
		public Guid AggregateRootId;
		public int LatestVersion;
		public object AggregateRoot;

		public bool Equals(AggregateRootAndVersion other)
		{
			return AggregateRootId.Equals(other.AggregateRootId);
		}

		public bool Equals(Guid other)
		{
			return AggregateRootId.Equals(other);
		}

		public override bool Equals(object obj)
		{
			var o = obj as AggregateRootAndVersion;
			if (null == o)
			{
				return false;
			}
			return AggregateRootId == o.AggregateRootId;
		}

		public override int GetHashCode()
		{
			return AggregateRootId.GetHashCode();
		}
	}
}
