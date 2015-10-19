using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CmisSync.Lib
{
    public class ModelBase : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
                
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            this.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
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

        #endregion INotifyPropertyChanged

        protected void setPropertyValue<T>(string name, ref T property, T value)
        {
            if (!Object.Equals(property, value))
            {
                property = value;
                OnPropertyChanged(name);
            }
        }
    }
}
