using System.IO;
using System.Xml.Serialization;

namespace Utils.OData.Metadatas;

/// <summary>
/// Provides helper methods to deserialize EDMX metadata documents.
/// </summary>
public static class DeserializeMetadatas
{
    /// <summary>
    /// Gets the XML serializer configured for EDMX documents.
    /// </summary>
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

    /// <summary>
    /// Deserializes EDMX metadata from the provided XML stream.
    /// </summary>
    /// <param name="xml">Stream containing the EDMX XML document.</param>
    /// <returns>An <see cref="Edmx"/> instance when the stream contains valid metadata; otherwise <see langword="null"/>.</returns>
    public static Edmx? Deserialize(Stream xml)
    {
        return Serializer.Deserialize(xml) as Edmx;
    }
}
