using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Diagnostics;

namespace Utils.OData.Metadatas;

[XmlRoot(ElementName = "Edmx", Namespace = "http://docs.oasis-open.org/odata/ns/edmx")]
public class Edmx
{
    [XmlElement("DataServices", Namespace = "http://docs.oasis-open.org/odata/ns/edmx")]
    public DataServices[] DataServices { get; set; }
}

public class DataServices
{
    [XmlElement("Schema", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public Schema[] Schemas { get; set; }
}

public class Schema
{
    [XmlElement("EntityType", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public EntityType[] EntityTypes { get; set; }

    [XmlElement("Annotations", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public AnnotationsSet[] AnnotationsSets { get; set; }
}

[DebuggerDisplay("EntityType : {Name}")]
public class EntityType
{
    [XmlAttribute("Name", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public string Name { get; set; }

    [XmlElement("Key", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public Key Key { get; set; }

    [XmlElement("Property", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public Property[] Properties { get; set; }

}
public class Key
{
    [XmlElement("PropertyRef", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public PropertyRef[] PropertyRefs { get; set; }
}

[DebuggerDisplay("Key : {Name}")]
public class PropertyRef
{
    [XmlAttribute("Name", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public string Name { get; set; }
}

[DebuggerDisplay("Keys : {Name} {Type} {MaxLength}")]
public class Property
{
    [XmlAttribute("Name", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public string Name { get; set; }

    [XmlAttribute("Type", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public string Type { get; set; }

    [XmlAttribute("MaxLength", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public int MaxLength { get; set; }

    [XmlElement("Annotation", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public Annotation[] Annotations { get; set; }
}

public class AnnotationsSet
{
    [XmlElement("Annotation", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public Annotation[] Annotations { get; set; }
}

[DebuggerDisplay("Keys : {Term} {String} {Bool}")]
public class Annotation
{
    [XmlAttribute("Term", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public string Term { get; set; }

    [XmlAttribute("String", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public string String { get; set; }

    [XmlAttribute("Bool", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public bool Bool { get; set; }

    [XmlElement("EnumMember", Namespace = "http://docs.oasis-open.org/odata/ns/edm")]
    public EnumMember[] EnumMembers { get; set; }
}

[DebuggerDisplay("EnumMember : {Value}")]
public class EnumMember
{
    [XmlText]
    public string Value { get; set; }
}

