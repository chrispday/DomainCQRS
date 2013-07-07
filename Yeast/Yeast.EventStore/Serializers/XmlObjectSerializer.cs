using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Serializers
{
	public class XmlObjectSerializer : IEventSerializer
	{
		public System.Runtime.Serialization.XmlObjectSerializer Serializer { get; set; }

		public T Deserialize<T>(System.IO.Stream serializationStream)
		{
			return (T)Serializer.ReadObject(serializationStream);
		}

		public T Serialize<T>(System.IO.Stream serializationStream, T graph)
		{
			Serializer.WriteObject(serializationStream, graph);
			return graph;
		}
	}
}
