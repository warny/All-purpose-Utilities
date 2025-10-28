using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Utils.OData.Metadatas;

public static class DeserializeMetadatas
{
	private static XmlSerializer Serializer { get; } = XmlSerializer.FromTypes([
		typeof(Edmx),
		/*typeof(DataServices),
		typeof(Schema),
		typeof(EntityType),
		typeof(Key),
		typeof(PropertyRef),
		typeof(Property),
		typeof(AnnotationsSet),
		typeof(Annotation),
		typeof(EnumMember)*/
	])[0];

	public static Edmx Deserialize(Stream xml)
	{

		return Serializer.Deserialize(xml) as Edmx; 
	}
}
