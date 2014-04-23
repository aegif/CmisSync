using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using log4net;

namespace CmisSync.Lib.Events
{
    /// <summary>
    /// Active activities manager.
    /// </summary>
    public class ActiveActivitiesManager
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ActiveActivitiesManager));

        private object Lock = new object();

        private ObservableCollection<FileTransmissionEvent> activeTransmissions = new ObservableCollection<FileTransmissionEvent>();

        /// <summary>
        /// Gets the active transmissions. This Collection can be obsered for changes.
        /// </summary>
        /// <value>
        /// The active transmissions.
        /// </value>
        public ObservableCollection<FileTransmissionEvent> ActiveTransmissions { get { return activeTransmissions; } }

        /// <summary>
        /// Add a new Transmission to the active transmission manager
        /// </summary>
        /// <param name="transmission"></param>
        public bool AddTransmission(FileTransmissionEvent transmission) {
            lock (Lock)
            {
                if(!activeTransmissions.Contains(transmission)) {
                    transmission.TransmissionStatus += TransmissionFinished;
                    activeTransmissions.Add(transmission);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// If a transmission is reported as finished/aborted/failed, the transmission is removed from the collection
        /// </summary>
        /// <param name='sender'>
        /// The transmission event.
        /// </param>
        /// <param name='e'>
        /// The progress parameters of the transmission.
        /// </param>
        private void TransmissionFinished(object sender, TransmissionProgressEventArgs e)
        {
            if ((e.Aborted == true || e.Completed == true || e.FailedException != null))
            {
                lock (Lock)
                {
                    FileTransmissionEvent transmission = sender as FileTransmissionEvent;
                    if(transmission!=null && activeTransmissions.Contains(transmission)) {
                        activeTransmissions.Remove(transmission);
                        transmission.TransmissionStatus-=TransmissionFinished;
                        Logger.Debug("Transmission removed");
                    }
                }
            }
        }
    }
}
