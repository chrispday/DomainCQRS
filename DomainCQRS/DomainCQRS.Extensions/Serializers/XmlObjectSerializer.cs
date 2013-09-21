using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	public static class XmlObjectSerializerConfigure
	{
		public static IConfigure XmlObjectSerializer(this IConfigure configure, System.Runtime.Serialization.XmlObjectSerializer serializer)
		{
			(configure as Configure).EventSerializer = new XmlObjectSerializer() { Serializer = serializer };
			return configure;
		}
	}

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
