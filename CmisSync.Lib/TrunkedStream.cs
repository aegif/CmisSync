using System;
using System.Diagnostics;
using System.IO;

namespace CmisSync.Lib
{
    public class TrunkedStream : Stream
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

        private long trunkPosition;
        public long TrunkPosition
        {
            get
            {
                return trunkPosition;
            }

            set
            {
                source.Position = value;
                trunkPosition = value;
            }
        }

        public override long Length
        {
            get
            {
                long lengthSource = source.Length;
                if (lengthSource <= TrunkPosition)
                {
                    return 0;
                }

                long length = lengthSource - TrunkPosition;
                if (length >= trunkSize)
                {
                    return trunkSize;
                }
                else
                {
                    return length;
                }
            }
        }

        public override long Position
        {
            get
            {
                long position = source.Position - TrunkPosition;
                if (position < 0 || position > trunkSize)
                {
                    Debug.Assert(false, String.Format("Position {0} not in [0,{1}]", position, trunkSize));
                }
                return position;
            }

            set
            {
                if (value < 0 || value > trunkSize)
                {
                    throw new System.ArgumentOutOfRangeException(String.Format("Position {0} not in [0,{1}]", value, trunkSize));
                }
                source.Position = TrunkPosition + value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0)
            {
                throw new System.ArgumentOutOfRangeException("offset", offset, "offset is negative");
            }
            if (count < 0)
            {
                throw new System.ArgumentOutOfRangeException("count", count, "count is negative");
            }

            if (count > trunkSize - Position)
            {
                count = (int)(trunkSize - Position);
            }
            return source.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset < 0)
            {
                throw new System.ArgumentOutOfRangeException("offset", offset, "offset is negative");
            }
            if (count < 0)
            {
                throw new System.ArgumentOutOfRangeException("count", count, "count is negative");
            }

            if (count > trunkSize - Position)
            {
                throw new System.ArgumentOutOfRangeException("count", count, "count is overflow");
            }
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
