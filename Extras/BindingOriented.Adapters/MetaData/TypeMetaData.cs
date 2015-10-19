using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BindingOriented.Adapters.MetaData
{
    /// <summary>
    /// Represents the meta-data for a given type after it has been reflected.
    /// </summary>
    internal sealed class TypeMetaData
    {
        private List<PropertyInfo> _allKnownProperties;
        private List<PropertyDescriptor> _propertyDescriptors;
        private Dictionary<PropertyInfo, IDelegatePropertyReader> _propertyReaders;
        private Dictionary<PropertyInfo, IDelegatePropertyWriter> _propertyWriters;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditableWrapperMetaData"/> class.
        /// </summary>
        public TypeMetaData()
        {
            _allKnownProperties = new List<PropertyInfo>();
            _propertyDescriptors = new List<PropertyDescriptor>();
            _propertyReaders = new Dictionary<PropertyInfo, IDelegatePropertyReader>();
            _propertyWriters = new Dictionary<PropertyInfo, IDelegatePropertyWriter>();
        }

        /// <summary>
        /// Gets all known properties.
        /// </summary>
        /// <value>All known properties.</value>
        public List<PropertyInfo> AllKnownProperties 
        { 
            get { return _allKnownProperties; } 
        }

        /// <summary>
        /// Gets the readers.
        /// </summary>
        /// <value>The readers.</value>
        public Dictionary<PropertyInfo, IDelegatePropertyReader> PropertyReaders 
        { 
            get { return _propertyReaders; } 
        }

        /// <summary>
        /// Gets the writers.
        /// </summary>
        /// <value>The writers.</value>
        public Dictionary<PropertyInfo, IDelegatePropertyWriter> PropertyWriters
        {
            get { return _propertyWriters; }
        }

        /// <summary>
        /// Gets the property descriptors.
        /// </summary>
        /// <value>The property descriptors.</value>
        public List<PropertyDescriptor> PropertyDescriptors
        {
            get { return _propertyDescriptors; }
        }
    }
}
