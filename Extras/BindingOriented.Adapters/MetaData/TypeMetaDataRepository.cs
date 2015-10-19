using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.ComponentModel;

namespace BindingOriented.Adapters.MetaData
{
    /// <summary>
    /// Creates and stores the metadata for a 
    /// </summary>
    internal static class TypeMetaDataRepository
    {
        private static Dictionary<Type, TypeMetaData> _contexts = new Dictionary<Type, TypeMetaData>();
        private static readonly object _contextsLock = new object();

        /// <summary>
        /// Creates the context.
        /// </summary>
        /// <returns></returns>
        public static TypeMetaData GetFor(Type wrapperObjectType, Type wrappedObjectType)
        {
            lock (_contextsLock)
            {
                if (!_contexts.ContainsKey(wrapperObjectType))
                {
                    TypeMetaData result = new TypeMetaData();

                    List<string> propertyNames = new List<string>();
                    foreach (PropertyInfo property in wrapperObjectType.GetProperties())
                    {
                        if (property.GetCustomAttributes(true).Where(a => a is BrowsableAttribute && ((BrowsableAttribute)a).Browsable == false).Count() == 0)
                        {
                            result.PropertyDescriptors.Add(CreateWrapperObjectPropertyWrapper(result, property));
                            result.AllKnownProperties.Add(property);
                            propertyNames.Add(property.Name);
                        }
                    }

                    foreach (PropertyInfo property in wrappedObjectType.GetProperties())
                    {
                        if (!propertyNames.Contains(property.Name))
                        {
                            result.PropertyDescriptors.Add(CreateWrappedObjectPropertyWrapper(result, property));
                            result.AllKnownProperties.Add(property);
                        }
                    }
                    _contexts.Add(wrapperObjectType, result);
                }
                return _contexts[wrapperObjectType];
            }
        }

        /// <summary>
        /// Creates the wrapped object property wrapper.
        /// </summary>
        private static PropertyDescriptor CreateWrappedObjectPropertyWrapper(TypeMetaData context, PropertyInfo property)
        {
            IDelegatePropertyReader actualReader = GetPropertyReader(property, context);
            IDelegatePropertyWriter actualWriter = GetPropertyWriter(property, context);

            Func<object, object> reader = property.CanRead ? new Func<object, object>(o => ((IEditable)o).ReadProperty(property)) : null;
            Action<object, object> writer = property.CanWrite ? new Action<object, object>((o, v) => ((IEditable)o).WriteProperty(property, v)) : null;

            DelegatePropertyDescriptor descriptor = new DelegatePropertyDescriptor(
                property.Name,
                property.DeclaringType,
                property.PropertyType,
                reader,
                writer
                );
            return descriptor;
        }

        /// <summary>
        /// Creates the wrapper object property wrapper.
        /// </summary>
        private static PropertyDescriptor CreateWrapperObjectPropertyWrapper(TypeMetaData context, PropertyInfo property)
        {
            IDelegatePropertyReader actualReader = GetPropertyReader(property, context);
            IDelegatePropertyWriter actualWriter = GetPropertyWriter(property, context);

            Func<object, object> reader = property.CanRead ? new Func<object, object>(o => actualReader.GetValue(o)) : null;
            Action<object, object> writer = property.CanWrite ? new Action<object, object>((o, v) => actualWriter.SetValue(o, v)) : null;

            DelegatePropertyDescriptor descriptor = new DelegatePropertyDescriptor(
                property.Name,
                property.DeclaringType,
                property.PropertyType,
                reader,
                writer
                );
            return descriptor;
        }

        /// <summary>
        /// Gets the property reader.
        /// </summary>
        private static IDelegatePropertyReader GetPropertyReader(PropertyInfo property, TypeMetaData context)
        {
            IDelegatePropertyReader actualReader = DelegatePropertyFactory.CreatePropertyReader(property);
            if (actualReader != null)
            {
                context.PropertyReaders.Add(property, actualReader);
            }
            return actualReader;
        }

        /// <summary>
        /// Gets the property writer.
        /// </summary>
        private static IDelegatePropertyWriter GetPropertyWriter(PropertyInfo property, TypeMetaData context)
        {
            IDelegatePropertyWriter actualWriter = DelegatePropertyFactory.CreatePropertyWriter(property);
            if (actualWriter != null)
            {
                context.PropertyWriters.Add(property, actualWriter);
            }
            return actualWriter;
        }
    }
}
