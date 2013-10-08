using System;
using log4net;
using System.IO;

namespace CmisSync.Lib
{
    public class LoggingStream : Stream
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LoggingStream));
        private string prefix = "";
        private Stream stream;
        private bool isDebuggingEnabled = Logger.IsDebugEnabled;
        private long length;
        private long readpos = 0;
        private long writepos = 0;
        public LoggingStream(Stream stream, string prefix, string filename, long streamlength)
        {
            this.stream = stream;
            this.length = streamlength;
            this.prefix = String.Format("{0} {1}: ", prefix, filename, Utils.FormatSize(this.length));
        }
        public override bool CanRead
        {
            get
            {
                return this.stream.CanRead;
            }
        }
        public override bool CanSeek
        {
            get
            {
                return this.stream.CanSeek;
            }
        }
        public override bool CanWrite
        {
            get
            {
                return this.stream.CanWrite;
            }
        }
        public override long Length
        {
            get
            {
                return this.stream.Length;
            }
        }
        public override long Position
        {
            get
            {
                return this.stream.Position;
            }
            set
            {
                this.stream.Position = value;
            }
        }
        public override void Flush()
        {
            if(isDebuggingEnabled)
                Logger.Debug("Flushing stream");
            this.stream.Flush();
        }
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if(isDebuggingEnabled)
                Logger.Debug(String.Format("{0}BeginRead(..., int offset={1}, int count={2}, ...)", prefix, offset, count));
            return this.stream.BeginRead(buffer, offset, count, callback, state);
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            if(isDebuggingEnabled)
                Logger.Debug(String.Format("{0}Seek(long offset={1}, ...)", prefix, offset));
            return this.stream.Seek(offset,origin);
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if(isDebuggingEnabled) {
                int result = this.stream.Read(buffer,offset,count);
                readpos+=result;
                long percentage = (readpos * 100)/ (this.length>0?this.length:100);
                Logger.Debug(String.Format("{0}% {1} of {2}",
                                           percentage,
                                           Utils.FormatSize(this.readpos),
                                           Utils.FormatSize(this.length)));
                return result;
            }
            else
                return this.stream.Read(buffer,offset,count);
        }
        public override void SetLength(long value)
        {
            if(isDebuggingEnabled)
                Logger.Debug(String.Format("{0}SetLength(long value={1})", prefix, value));
            this.stream.SetLength(value);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.stream.Write(buffer, offset, count);
            if(isDebuggingEnabled)
            {
                writepos += count;
                long percentage = (writepos * 100)/ (this.length>0?this.length:100);
                Logger.Debug(String.Format("{0}% {1} of {2})",
                                           percentage,
                                           Utils.FormatSize(this.writepos),
                                           Utils.FormatSize(this.length)));
            }
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}

