using System;
using System.Diagnostics;
using System.IO;

namespace CmisSync.Lib
{
    /// <summary></summary>
    public class ChunkedStream : Stream
    {
        private Stream source;
        private long chunkSize;

        /// <summary></summary>
        /// <param name="stream"></param>
        /// <param name="chunk"></param>
        public ChunkedStream(Stream stream, long chunk)
        {
            source = stream;
            chunkSize = chunk;

            //if (!source.CanRead)
            //{
            //    throw new System.NotSupportedException("Read access is needed for ChunkedStream");
            //}
        }

        /// <summary></summary>
        public override bool CanRead { get { return source.CanRead; } }

        /// <summary></summary>
        public override bool CanWrite { get { return source.CanWrite; } }

        /// <summary></summary>
        public override bool CanSeek { get { return source.CanSeek; } }

        /// <summary></summary>
        public override void Flush() { source.Flush(); }

        private long chunkPosition;

        /// <summary></summary>
        public long ChunkPosition
        {
            get
            {
                return chunkPosition;
            }

            set
            {
                source.Position = value;
                chunkPosition = value;
            }
        }

        /// <summary></summary>
        public override long Length
        {
            get
            {
                long lengthSource = source.Length;
                if (lengthSource <= ChunkPosition)
                {
                    return 0;
                }

                long length = lengthSource - ChunkPosition;
                if (length >= chunkSize)
                {
                    return chunkSize;
                }
                else
                {
                    return length;
                }
            }
        }

        private long position;

        /// <summary></summary>
        public override long Position
        {
            get
            {
                if (!CanSeek)
                {
                    return position;
                }

                long offset = source.Position - ChunkPosition;
                if (offset < 0 || offset > chunkSize)
                {
                    Debug.Assert(false, String.Format("Position {0} not in [0,{1}]", offset, chunkSize));
                }
                return offset;
            }

            set
            {
                if (value < 0 || value > chunkSize)
                {
                    throw new System.ArgumentOutOfRangeException(String.Format("Position {0} not in [0,{1}]", value, chunkSize));
                }
                source.Position = ChunkPosition + value;
            }
        }

        /// <summary></summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
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

            if (count > chunkSize - Position)
            {
                count = (int)(chunkSize - Position);
            }
            count = source.Read(buffer, offset, count);
            position += count;
            return count;
        }

        /// <summary></summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
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

            if (count > chunkSize - Position)
            {
                throw new System.ArgumentOutOfRangeException("count", count, "count is overflow");
            }
            source.Write(buffer, offset, count);
            position += count;
        }

        /// <summary></summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            Debug.Assert(false, "TODO");
            return source.Seek(offset, origin);
        }

        /// <summary></summary>
        /// <param name="value"></param>
        public override void SetLength(long value)
        {
            Debug.Assert(false, "TODO");
            source.SetLength(value);
        }
    }
}
