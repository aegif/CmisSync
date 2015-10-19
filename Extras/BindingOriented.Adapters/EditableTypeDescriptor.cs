using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using BindingOriented.Adapters.MetaData;

namespace BindingOriented.Adapters
{
    /// <summary>
    /// A type descriptor for EditableWrapper objects that enable data binding.
    /// </summary>
    public sealed class EditableTypeDescriptor : CustomTypeDescriptor
    {
        private TypeMetaData _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="DesignTimeTypeDescriptor"/> class.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="instance">The instance.</param>
        public EditableTypeDescriptor(Type objectType)
        {
            Type wrapperObjectType = objectType;
            Type wrappedObjectType = objectType;
            Type baseType = wrapperObjectType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                if (baseType.Name == typeof(EditableAdapter<>).Name 
                    && baseType.GetGenericArguments().Length > 0)
                {
                    wrappedObjectType = baseType.GetGenericArguments()[0];
                    break;
                }
                baseType = baseType.BaseType;
            }
            _context = TypeMetaDataRepository.GetFor(wrapperObjectType, wrappedObjectType);
        }

        /// <summary>
        /// Returns the properties for this instance of a component.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.ComponentModel.PropertyDescriptorCollection"/> that represents the properties for this component instance.
        /// </returns>
        public override PropertyDescriptorCollection GetProperties()
        {
            return new PropertyDescriptorCollection(_context.PropertyDescriptors.ToArray());
        }

        /// <summary>
        /// Returns the properties for this instance of a component using the attribute array as a filter.
        /// </summary>
        /// <param name="attributes">An array of type <see cref="T:System.Attribute"/> that is used as a filter.</param>
        /// <returns>
        /// A <see cref="T:System.ComponentModel.PropertyDescriptorCollection"/> that represents the filtered properties for this component instance.
        /// </returns>
        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            List<PropertyDescriptor> descriptors = new List<PropertyDescriptor>();
            foreach (PropertyDescriptor descriptor in this.GetProperties())
            {
                bool include = true;
                foreach (Attribute searchAttribute in attributes)
                {
                    if (descriptor.Attributes.Contains(searchAttribute))
                    {
                        include = true;
                        break;
                    }
                }
                if (include)
                {
                    descriptors.Add(descriptor);
                }
            }
            return new PropertyDescriptorCollection(descriptors.ToArray());
        }
    }
}
