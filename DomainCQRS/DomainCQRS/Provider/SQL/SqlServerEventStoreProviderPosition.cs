﻿using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Provider
{
	public class SqlServerEventStoreProviderPosition : IEventStoreProviderPosition
	{
		public long Position;

		public override string ToString()
		{
			return Position.ToString();
		}
	}
}