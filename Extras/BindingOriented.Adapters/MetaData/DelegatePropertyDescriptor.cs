using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace BindingOriented.Adapters.MetaData
{
    /// <summary>
    /// A property descriptor that uses delegates to read and write the properties. 
    /// </summary>
    internal class DelegatePropertyDescriptor : PropertyDescriptor
    {
        private Func<object, object> _getValueCallback;
        private Action<object, object> _setValueCallback;
        private Type _componentType;
        private Type _propertyType;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatedPropertyDescriptor"/> class.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="componentType">Type of the component.</param>
        /// <param name="propertyType">Type of the property.</param>
        /// <param name="getValueCallback">The get value callback.</param>
        /// <param name="setValueCallback">The set value callback.</param>
        public DelegatePropertyDescriptor(string propertyName, Type componentType, Type propertyType, Func<object, object> getValueCallback, Action<object, object> setValueCallback)
            : base(propertyName, null)
        {
            _getValueCallback = getValueCallback;
            _setValueCallback = setValueCallback;
            _componentType = componentType;
            _propertyType = propertyType;
        }

        /// <summary>
        /// When overridden in a derived class, returns whether resetting an object changes its value.
        /// </summary>
        /// <param name="component">The component to test for reset capability.</param>
        /// <returns>
        /// true if resetting the component changes its value; otherwise, false.
        /// </returns>
        public override bool CanResetValue(object component)
        {
            return false;
        }

        /// <summary>
        /// When overridden in a derived class, gets the type of the component this property is bound to.
        /// </summary>
        /// <value></value>
        /// <returns>A <see cref="T:System.Type"/> that represents the type of component this property is bound to. When the <see cref="M:System.ComponentModel.PropertyDescriptor.GetValue(System.Object)"/> or <see cref="M:System.ComponentModel.PropertyDescriptor.SetValue(System.Object,System.Object)"/> methods are invoked, the object specified might be an instance of this type.</returns>
        public override Type ComponentType
        {
            get { return _componentType; }
        }

        /// <summary>
        /// When overridden in a derived class, gets the current value of the property on a component.
        /// </summary>
        /// <param name="component">The component with the property for which to retrieve the value.</param>
        /// <returns>
        /// The value of a property for a given component.
        /// </returns>
        public override object GetValue(object component)
        {
            return _getValueCallback(component);
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether this property is read-only.
        /// </summary>
        /// <value></value>
        /// <returns>true if the property is read-only; otherwise, false.</returns>
        public override bool IsReadOnly
        {
            get { return _setValueCallback == null; }
        }

        /// <summary>
        /// When overridden in a derived class, gets the type of the property.
        /// </summary>
        /// <value></value>
        /// <returns>A <see cref="T:System.Type"/> that represents the type of the property.</returns>
        public override Type PropertyType
        {
            get { return _propertyType; }
        }

        /// <summary>
        /// When overridden in a derived class, resets the value for this property of the component to the default value.
        /// </summary>
        /// <param name="component">The component with the property value that is to be reset to the default value.</param>
        public override void ResetValue(object component)
        {

        }

        /// <summary>
        /// When overridden in a derived class, sets the value of the component to a different value.
        /// </summary>
        /// <param name="component">The component with the property value that is to be set.</param>
        /// <param name="value">The new value.</param>
        public override void SetValue(object component, object value)
        {
            if (_setValueCallback != null)
            {
                _setValueCallback(component, value);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// When overridden in a derived class, determines a value indicating whether the value of this property needs to be persisted.
        /// </summary>
        /// <param name="component">The component with the property to be examined for persistence.</param>
        /// <returns>
        /// true if the property should be persisted; otherwise, false.
        /// </returns>
        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }
    }
}
