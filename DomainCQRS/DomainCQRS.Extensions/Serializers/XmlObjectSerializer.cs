using System;
using System.Collections.Generic;
using System.Text;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class XmlObjectSerializerConfigure
	{
		public static IConfigure XmlObjectSerializer(this IConfigure configure, System.Runtime.Serialization.XmlObjectSerializer serializer)
		{
			configure.Registry.BuildInstancesOf<System.Runtime.Serialization.XmlObjectSerializer>()
				.TheDefaultIs(Registry.Object<System.Runtime.Serialization.XmlObjectSerializer>(serializer))
				.AsSingletons();
			configure.Registry
				.BuildInstancesOf<IEventSerializer>()
				.TheDefaultIsConcreteType<XmlObjectSerializer>();
			return configure;
		}
	}

	public class XmlObjectSerializer : IEventSerializer
	{
		private readonly System.Runtime.Serialization.XmlObjectSerializer _serializer;

		public XmlObjectSerializer(System.Runtime.Serialization.XmlObjectSerializer serializer)
		{
			if (null == serializer)
			{
				throw new ArgumentNullException("serializer");
			}

			_serializer = serializer;
		}

		public T Deserialize<T>(System.IO.Stream serializationStream)
		{
			return (T)_serializer.ReadObject(serializationStream);
		}

		public T Serialize<T>(System.IO.Stream serializationStream, T graph)
		{
			_serializer.WriteObject(serializationStream, graph);
			return graph;
		}
	}
}
