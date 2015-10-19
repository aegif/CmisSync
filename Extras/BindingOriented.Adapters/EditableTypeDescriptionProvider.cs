using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace BindingOriented.Adapters
{
    /// <summary>
    /// A TypeDescriptionProvider that provides TypeDescriptor information for EditableWrapper objects.
    /// </summary>
    public sealed class EditableTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static Dictionary<Type, EditableTypeDescriptor> _alreadyCreated = new Dictionary<Type, EditableTypeDescriptor>();
        private readonly object _alreadyCreatedLock = new object();

        /// <summary>
        /// Creates an object that can substitute for another data type.
        /// </summary>
        /// <param name="provider">An optional service provider.</param>
        /// <param name="objectType">The type of object to create. This parameter is never null.</param>
        /// <param name="argTypes">An optional array of types that represent the parameter types to be passed to the object's constructor. This array can be null or of zero length.</param>
        /// <param name="args">An optional array of parameter values to pass to the object's constructor.</param>
        /// <returns>
        /// The substitute <see cref="T:System.Object"/>.
        /// </returns>
        public override object CreateInstance(IServiceProvider provider, Type objectType, Type[] argTypes, object[] args)
        {
            return base.CreateInstance(provider, objectType, argTypes, args);
        }

        /// <summary>
        /// Gets a custom type descriptor for the given type and object.
        /// </summary>
        /// <param name="objectType">The type of object for which to retrieve the type descriptor.</param>
        /// <param name="instance">An instance of the type. Can be null if no instance was passed to the <see cref="T:System.ComponentModel.TypeDescriptor"/>.</param>
        /// <returns>
        /// An <see cref="T:System.ComponentModel.ICustomTypeDescriptor"/> that can provide metadata for the type.
        /// </returns>
        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            lock (_alreadyCreatedLock)
            {
                if (!_alreadyCreated.ContainsKey(objectType))
                {
                    _alreadyCreated.Add(objectType, new EditableTypeDescriptor(objectType));
                }
                return _alreadyCreated[objectType];
            }
        }
    }
}
