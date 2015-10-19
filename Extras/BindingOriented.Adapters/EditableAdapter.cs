using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using BindingOriented.Adapters;
using BindingOriented.Adapters.MetaData;

namespace BindingOriented.Adapters
{
    /// <summary>
    /// Provides a bindable wrapper around domain objects providing IEditableObject, INotifyPropertyChanged 
    /// and IDataErrorInfo implementations.
    /// </summary>
    /// <typeparam name="TDomainObject">The type of the domain object.</typeparam>
    [TypeDescriptionProvider(typeof(EditableTypeDescriptionProvider))]
    public class EditableAdapter<TWrappedObject> :
        INotifyPropertyChanged,
        IEditableObject,
        IEditable
    {
        private TWrappedObject _current;
        private Dictionary<PropertyInfo, object> _changedProperties;
        private TypeMetaData _metaData;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditableAdapter&lt;TWrappedObject&gt;"/> class.
        /// </summary>
        /// <param name="current">The object being wrapped.</param>
        public EditableAdapter(TWrappedObject current)
        {
            _changedProperties = new Dictionary<PropertyInfo, object>();
            _current = current;
            _metaData = TypeMetaDataRepository.GetFor(this.GetType(), typeof(TWrappedObject));
        }

        /// <summary>
        /// Gets the current item.
        /// </summary>
        public TWrappedObject WrappedInstance
        {
            get { return _current; }
        }

        /// <summary>
        /// Gets the current item.
        /// </summary>
        object IEditable.WrappedInstance
        {
            get { return _current; }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets a value indicating whether this instance has changes.
        /// </summary>
        public bool HasChanges
        {
            get { return _changedProperties.Count > 0; }
        }

        /// <summary>
        /// Begins an edit on an object.
        /// </summary>
        public void BeginEdit()
        {
        }

        /// <summary>
        /// Discards changes since the last <see cref="M:System.ComponentModel.IEditableObject.BeginEdit"/> call.
        /// </summary>
        public void CancelEdit()
        {
            _changedProperties.Clear();
            if (_metaData != null)
            {
                _metaData.AllKnownProperties.ForEach(p => OnPropertyChanged(new PropertyChangedEventArgs(p.Name)));
            }
            this.OnPropertyChanged(new PropertyChangedEventArgs("HasChanges"));
        }

        /// <summary>
        /// Pushes changes since the last <see cref="M:System.ComponentModel.IEditableObject.BeginEdit"/> or <see cref="M:System.ComponentModel.IBindingList.AddNew"/> call into the underlying object.
        /// </summary>
        public void EndEdit()
        {
            if (_metaData != null)
            {
                foreach (KeyValuePair<PropertyInfo, object> property in _changedProperties)
                {
                    if (property.Key.CanWrite)
                    {
                        _metaData.PropertyWriters[property.Key].SetValue(_current, property.Value);
                    }
                }
                _changedProperties.Clear();
                _metaData.AllKnownProperties.ForEach(p => OnPropertyChanged(new PropertyChangedEventArgs(p.Name)));
                this.OnPropertyChanged(new PropertyChangedEventArgs("HasChanges"));
            }
        }

        /// <summary>
        /// Reads the property.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="property">The property.</param>
        /// <returns></returns>
        object IEditable.ReadProperty(PropertyInfo property)
        {
            object result = null;
            if (property.CanRead)
            {
                if (_changedProperties.ContainsKey(property))
                {
                    result = _changedProperties[property];
                }
                else
                {
                    result = _metaData.PropertyReaders[property].GetValue(_current);
                }
            }
            return result;
        }

        /// <summary>
        /// Writes the property.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        void IEditable.WriteProperty(PropertyInfo property, object value)
        {
            if (property.CanWrite)
            {
                if (property.GetValue(_current, null) == null || !property.GetValue(_current, null).Equals(value))
                {
                    if (!_changedProperties.ContainsKey(property))
                    {
                        _changedProperties.Add(property, value);
                    }
                    else
                    {
                        _changedProperties[property] = value;
                    }
                }
                else
                {
                    if (_changedProperties.ContainsKey(property))
                    {
                        _changedProperties.Remove(property);
                    }
                }

                this.OnPropertyChanged(new PropertyChangedEventArgs(property.Name));
                this.OnPropertyChanged(new PropertyChangedEventArgs("HasChanges"));
            }
        }

        /// <summary>
        /// Gets the edited value of a property, even if the value has not yet been committed to the underlying item.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns></returns>
        protected object ReadUncommitted(string propertyName)
        {
            PropertyInfo property = this.WrappedInstance.GetType().GetProperty(propertyName);
            if (property != null)
            {
                return ((IEditable)this).ReadProperty(property);
            }
            else
            {
                throw new Exception(string.Format("{0}.ReadUncommitted was called for property '{1}' which could not be found on the object '{2}'",
                    this.GetType().Name, 
                    propertyName,
                    this.WrappedInstance
                    ));
            }
        }

        /// <summary>
        /// Raises the <see cref="E:PropertyChanged"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.ComponentModel.PropertyChangedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, e);
            }
        }
    }
}
