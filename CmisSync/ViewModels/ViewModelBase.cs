using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;

namespace CmisSync.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged, IDataErrorInfo
    {
        private Controller _controller;
        protected Controller Controller { get{ return _controller;} }
        
        public event PropertyChangedEventHandler PropertyChanged;

        public ViewModelBase(Controller controller)
        {
            if (controller == null) {
                throw new System.InvalidOperationException();
            }
            this._controller = controller;
        }

        protected void NotifyOfPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #region IDataErrorInfo

        protected Dictionary<String, String> errors = new Dictionary<string, string>();

        string IDataErrorInfo.Error
        {
            get
            {
                if (errors.Count == 0)
                    return null;

                StringBuilder errorList = new StringBuilder();
                var errorMessages = errors.Values.GetEnumerator();
                while (errorMessages.MoveNext())
                    errorList.AppendLine(errorMessages.Current);

                return errorList.ToString();
            }
        }

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                string msg = validate(columnName);
                if (!String.IsNullOrEmpty(msg))
                {
                    errors.Add(columnName, msg);
                }
                else
                {
                    errors.Remove(columnName);
                }
                NotifyOfPropertyChanged("Error");
                return msg;
            }
        }

        protected virtual string validate(string columnName) 
        {
            return string.Empty;
        }
        
        public Boolean isValid(string columnName) {
            if(errors.ContainsKey(columnName)){
                return false;
            }
            return !String.IsNullOrEmpty(validate(columnName));
        }

        public Boolean isValid()
        {
            return errors.Count == 0;
        }

        #endregion IDataErrorInfo
    }
}
