using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Events
{
    public class FileTransmissionEvent: ISyncEvent
    {
        public FileTransmissionType Type { get; private set; }

        public string Path { get; private set; }

        public delegate void TransmissionEventHandler(object sender, TransmissionProgressEventArgs e);

        public event TransmissionEventHandler TransmissionStatus = delegate { };

        public FileTransmissionEvent(FileTransmissionType type, string path)
        {
            if(path == null) {
                throw new ArgumentNullException("Argument null in FSEvent Constructor","path");
            }
            Type = type;
            Path = path;
        }

        public override string ToString()
        {
            return string.Format("FileTransmissionEvent with type \"{0}\" on path \"{1}\"", Type, Path);
        }

        private void ReportProgress()
        {
            // TODO must be implemented
            if (TransmissionStatus != null)
                TransmissionStatus(this, new TransmissionProgressEventArgs() {
                    BitsPerSecond = 0,
                    Percent = 100,
                    Length = 0,
                    ActualPosition = 0
                });
            throw new NotImplementedException("Reporing Status is not implemented yet");
        }
    }

    public class TransmissionProgressEventArgs
    {
        public long BitsPerSecond { get; set; }
        public double Percent { get; set; }
        public double Length { get; set; }
        public double ActualPosition { get; set; }
    }
    public enum FileTransmissionType
    {
        UPLOAD_NEW_FILE,
        UPLOAD_MODIFIED_FILE,
        DOWNLOAD_NEW_FILE,
        DOWNLOAD_MODIFIED_FILE
    }
}
