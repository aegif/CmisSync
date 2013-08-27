using System;
using System.Diagnostics;
using System.IO;

namespace CmisSync.Lib
{
    class TrunkedStream : Stream
    {
        private Stream source;
        private string sourceName;
        private Cmis.Database sourceDatabase;
        private long trunkSize;

        public TrunkedStream(Stream stream, string name, Cmis.Database database, long trunk)
        {
            source = stream;
            sourceName = name;
            sourceDatabase = database;
            trunkSize = trunk;

            if (!source.CanRead)
            {
                throw new System.NotSupportedException("Read access is needed for TrunkedStream");
            }
        }

        public override bool CanRead { get { return source.CanRead; } }
        public override bool CanWrite { get { return source.CanWrite; } }
        public override bool CanSeek { get { return source.CanSeek; } }
        public override void Flush() { source.Flush(); }

        public override long Length
        {
            get
            {
                Debug.Assert(false, "TODO");
                return source.Length;
            }
        }

        public override long Position
        {
            get
            {
                Debug.Assert(false, "TODO");
                return source.Position;
            }

            set
            {
                Debug.Assert(false, "TODO");
                source.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Debug.Assert(false, "TODO");
            return source.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Debug.Assert(false, "TODO");
            source.Write(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Debug.Assert(false, "TODO");
            return source.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Debug.Assert(false, "TODO");
            source.SetLength(value);
        }
    }
}
