using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Yeast.EventStore
{
	public static class BinaryFormatterSerializerConfigure
	{
		public static IConfigure BinaryFormatterSerializer(this IConfigure configure)
		{
			(configure as Configure).EventSerializer = new BinaryFormatterSerializer();
			return configure;
		}
	}

	public class BinaryFormatterSerializer : IEventSerializer
	{
		public BinaryFormatterSerializer()
		{
			Formatter = new BinaryFormatter();
		}

		public BinaryFormatter Formatter { get; set; }

		public T Deserialize<T>(System.IO.Stream serializationStream)
		{
			return (T)Formatter.Deserialize(serializationStream);
		}

		public T Serialize<T>(System.IO.Stream serializationStream, T graph)
		{
			Formatter.Serialize(serializationStream, graph);
			return graph;
		}
	}
}
