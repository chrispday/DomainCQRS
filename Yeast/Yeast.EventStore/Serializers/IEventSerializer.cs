using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Yeast.EventStore
{
	public interface IEventSerializer
	{
		T Deserialize<T>(Stream serializationStream);
		T Serialize<T>(Stream serializationStream, T graph);
	}
}
