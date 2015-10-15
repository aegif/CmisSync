using System;
using System.Collections.Generic;

using System.Xml.Serialization;
using System.Xml;

namespace CmisSync.Lib
{
    /// <summary>
    /// XML URI.
    /// </summary>
    public class XmlUri : IXmlSerializable
    {
        private Uri _value;

        /// <summary>
        /// Constructor.
        /// </summary>
        public XmlUri() { }

        /// <summary>
        /// Constructor.
        /// </summary>
        public XmlUri(Uri source) { _value = source; }

        /// <summary>
        /// implicit.
        /// </summary>
        public static implicit operator Uri(XmlUri o)
        {
            return o == null ? null : o._value;
        }

        /// <summary>
        /// implicit.
        /// </summary>
        public static implicit operator XmlUri(Uri o)
        {
            return o == null ? null : new XmlUri(o);
        }

        /// <summary>
        /// Get schema.
        /// </summary>
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        /// <summary>
        /// Read XML.
        /// </summary>
        public void ReadXml(XmlReader reader)
        {
            _value = new Uri(reader.ReadElementContentAsString());
        }

        /// <summary>
        /// Write XML.
        /// </summary>
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteValue(_value.ToString());
        }

        /// <summary>
        /// String representation of the URI.
        /// </summary>
        public override string ToString()
        {
            return _value.ToString();
        }

        //delegating methods

        // Summary:
        //     Gets the absolute path of the URI.
        //
        // Returns:
        //     A System.String containing the absolute path to the resource.
        //
        // Exceptions:
        //   System.InvalidOperationException:
        //     This instance represents a relative URI, and this property is valid only
        //     for absolute URIs.
        public string AbsolutePath { get { return _value.AbsolutePath; } }

        //
        // Summary:
        //     Gets the absolute URI.
        //
        // Returns:
        //     A System.String containing the entire URI.
        //
        // Exceptions:
        //   System.InvalidOperationException:
        //     This instance represents a relative URI, and this property is valid only
        //     for absolute URIs.
        public string AbsoluteUri { get { return _value.AbsoluteUri; } }

        //
        // Summary:
        //     Gets the specified portion of a System.Uri instance.
        //
        // Parameters:
        //   part:
        //     One of the System.UriPartial values that specifies the end of the URI portion
        //     to return.
        //
        // Returns:
        //     A System.String that contains the specified portion of the System.Uri instance.
        //
        // Exceptions:
        //   System.InvalidOperationException:
        //     The current System.Uri instance is not an absolute instance.
        //
        //   System.ArgumentException:
        //     The specified part is not valid.
        public string GetLeftPart(UriPartial part) { return _value.GetLeftPart(part); }
    }
}
