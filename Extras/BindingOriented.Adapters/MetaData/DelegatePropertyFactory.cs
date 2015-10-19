using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BindingOriented.Adapters.MetaData
{
    /// <summary>
    /// A factory to create <see cref="IDelegatePropertyReader">IDelegatePropertyReaders</see> and 
    /// <see cref="IDelegatePropertyWriter">IDelegatePropertyWriters</see> for properties. 
    /// </summary>
    internal static class DelegatePropertyFactory
    {
        /// <summary>
        /// Creates the property reader.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns></returns>
        public static IDelegatePropertyReader CreatePropertyReader(PropertyInfo property)
        {
            IDelegatePropertyReader reader = null;
            if (property != null)
            {
                Type delegateReaderType = typeof(Func<,>).MakeGenericType(property.DeclaringType, property.PropertyType);
                Type readerType = typeof(DelegatePropertyReader<,>).MakeGenericType(property.DeclaringType, property.PropertyType);
                if (property.CanRead)
                {
                    MethodInfo propertyGetterMethodInfo = property.GetGetMethod();
                    Delegate propertyGetterDelegate = Delegate.CreateDelegate(
                        delegateReaderType,
                        propertyGetterMethodInfo);
                    reader = (IDelegatePropertyReader)Activator.CreateInstance(readerType, propertyGetterDelegate);
                }
            }
            return reader;
        }

        /// <summary>
        /// Creates the property writer.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns></returns>
        public static IDelegatePropertyWriter CreatePropertyWriter(PropertyInfo property)
        {
            IDelegatePropertyWriter writer = null;
            if (property != null)
            {
                Type delegateWriterType = typeof(Action<,>).MakeGenericType(property.DeclaringType, property.PropertyType);
                Type writerType = typeof(DelegatePropertyWriter<,>).MakeGenericType(property.DeclaringType, property.PropertyType);
                if (property.CanWrite)
                {
                    MethodInfo propertySetterMethodInfo = property.GetSetMethod();
                    Delegate propertySetterDelegate = Delegate.CreateDelegate(
                        delegateWriterType,
                        propertySetterMethodInfo);
                    writer = (IDelegatePropertyWriter)Activator.CreateInstance(writerType, propertySetterDelegate);
                }
            }
            return writer;
        }
    }
}
