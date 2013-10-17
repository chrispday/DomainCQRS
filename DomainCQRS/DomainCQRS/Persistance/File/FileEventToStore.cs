using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS.Persister
{
	public class FileEventToStore : EventToStore
	{
		public int Size { get; set; }
	}
}
