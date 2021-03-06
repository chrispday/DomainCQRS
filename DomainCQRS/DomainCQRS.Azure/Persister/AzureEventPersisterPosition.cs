﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DomainCQRS.Azure.Persister
{
	[Serializable]
	public class AzureEventPersisterPosition : IEventPersisterPosition
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
