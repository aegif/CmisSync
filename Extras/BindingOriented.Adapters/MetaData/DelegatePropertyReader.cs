using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BindingOriented.Adapters.MetaData
{
    /// <summary>
    /// Interface implemented by the delegate property reader.
    /// </summary>
    internal interface IDelegatePropertyReader
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns></returns>
        object GetValue(object instance);
    }

    /// <summary>
    /// A class which wraps reading the values of properties.
    /// </summary>
    /// <typeparam name="TInstance">The type of the instance.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    internal class DelegatePropertyReader<TInstance, TProperty> : 
        IDelegatePropertyReader
    {
        private Func<TInstance, TProperty> _getValueDelegate;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatePropertyReader&lt;TInstance, TProperty, TCast&gt;"/> class.
        /// </summary>
        /// <param name="caller">The caller.</param>
        public DelegatePropertyReader(Func<TInstance, TProperty> getValueDelegate)
        {
            _getValueDelegate = getValueDelegate;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns></returns>
        public object GetValue(object instance)
        {
            return _getValueDelegate((TInstance)instance);
        }
    }
}
