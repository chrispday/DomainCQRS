using System;
using System.Collections.Generic;

using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DomainCQRS
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
		private readonly BinaryFormatter _formatter;
		private BinaryFormatter Formatter { get { return _formatter; } }
		public BinaryFormatterSerializer()
		{
			_formatter = new BinaryFormatter();
		}

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
