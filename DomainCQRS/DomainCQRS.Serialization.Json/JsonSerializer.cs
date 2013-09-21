using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DomainCQRS
{
	public static class JsonSerializerConfigure
	{
		public static IConfigure JsonSerializer(this IConfigure configure)
		{
			(configure as Configure).EventSerializer = new Serialization.JsonSerializer();
			return configure;
		}
	}
}

namespace DomainCQRS.Serialization
{
	public class JsonSerializer : IEventSerializer
	{
		public T Deserialize<T>(System.IO.Stream serializationStream)
		{
			return ServiceStack.Text.JsonSerializer.DeserializeFromReader<T>(new StreamReader(serializationStream));
		}

		public T Serialize<T>(System.IO.Stream serializationStream, T graph)
		{
			var writer = new StreamWriter(serializationStream);
			ServiceStack.Text.JsonSerializer.SerializeToWriter<T>(graph, writer);
			writer.Flush();
			return graph;
		}
	}
}
