using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Astc_Encoder_CSharp_Generator
{
    public class Comment 
    {
        [XmlAttribute(AttributeName = "Location")]
        public string Location { get; set; }

        [XmlText]
        public string Text { get; set; }
    }


    [XmlRoot(ElementName = "Struct")]
    public class Struct
    {

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "Comment")]
        public List<Comment> Comments { get; set; }

        [XmlElement(ElementName = "Field")]
        public List<Field> Fields { get; set; }
    }

    [XmlRoot(ElementName = "Constant")]
    public class Constant
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }
    }

    [XmlRoot(ElementName = "Enum")]
    public class Enum
    {

        [XmlElement(ElementName = "Comment")]
        public List<Comment> Comments { get; set; }

        [XmlElement(ElementName = "Constant")]
        public List<Constant> Constants { get; set; }

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }
    }

    [XmlRoot(ElementName = "Field")]
    public class Field
    {
        [XmlAttribute(AttributeName = "Prefix")]
        public string Prefix { get; set; }

        [XmlAttribute(AttributeName = "Type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Suffix")]
        public string Suffix { get; set; }
    }
    [XmlRoot(ElementName = "StaticField")]
    public class StaticField
    {
        [XmlAttribute(AttributeName = "Modifier")]
        public string Modifier { get; set; }

        [XmlAttribute(AttributeName = "Type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Initializer")]
        public string Initializer { get; set; }
    }

    [XmlRoot(ElementName = "FunctionPointer")]
    public class FunctionPointer
    {

        [XmlAttribute(AttributeName = "Return")]
        public string Return { get; set; }

        [XmlAttribute(AttributeName = "ReturnTypePrefix")]
        public string ReturnTypePrefix { get; set; }

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "Param")]
        public List<Field> Params { get; set; }
    }


    [XmlRoot(ElementName = "Method")]
    public class Method
    {

        [XmlElement(ElementName = "Param")]
        public List<Field> Param { get; set; }

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Return")]
        public string Return { get; set; }

        [XmlAttribute(AttributeName = "ReturnTypePrefix")]
        public string ReturnTypePrefix { get; set; }
    }

    [XmlRoot(ElementName = "Header")]
    public class Header
    {

        [XmlElement("Comment", typeof(Comment))]
        [XmlElement("Struct", typeof(Struct))]
        [XmlElement("Enum", typeof(Enum))]
        [XmlElement("StaticField", typeof(StaticField))]
        [XmlElement("FunctionPointer", typeof(FunctionPointer))]
        [XmlElement("Method", typeof(Method))]
        public List<object> Items { get; set; }
    }
}
