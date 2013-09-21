using System;
using System.Collections.Generic;
using System.IO;

using System.Runtime.Serialization;
using System.Text;

namespace DomainCQRS
{
	public interface IEventSerializer
	{
		T Deserialize<T>(Stream serializationStream);
		T Serialize<T>(Stream serializationStream, T graph);
	}
}
