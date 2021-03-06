﻿using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Persister
{
	public class MemoryEventPersisterPostion : IEventPersisterPosition
	{
		public Dictionary<Guid, int> Positions = new Dictionary<Guid, int>();

		public override string ToString()
		{
			if (0 == Positions.Count)
			{
				return "<Empty>";
			}

			var sb = new StringBuilder();
			foreach (var p in Positions)
			{
				sb.AppendFormat("{0} -> {1}", p.Key, p.Value).AppendLine();
			}
			return sb.ToString();
		}
	}
}
