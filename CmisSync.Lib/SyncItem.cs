using System;



namespace CmisSync.Lib
{
    abstract public class SyncItem
    {
        protected string path;

        public SyncItem(string path)
        {
            this.path = path;
        }

        abstract public string LocalPath
        {
            get;
        }
       
        abstract public string Path
        {
            get;
        }
    }


    public class LocalPathSyncItem : SyncItem
    {

        public LocalPathSyncItem(string path) : base(path) {}

        public override string LocalPath
        {
            get
            {
                return path;
            }
        }

        public override string Path
        {
            get
            {
                return PathRepresentationConverter.LocalToUtf8(path);
            }
        }
    }


    public class Utf8PathSyncItem : SyncItem
    {
        public Utf8PathSyncItem(string path) : base(path) {}

        public override string LocalPath
        {
            get
            {
                return PathRepresentationConverter.Utf8ToLocal(path);
            }
        }

        public override string Path
        {
            get
            {
                return path;
            }
        }
    }


    /// <summary>
    /// I path representation converter.
    /// </summary>
    public interface IPathRepresentationConverter
    {
        string LocalToUtf8(string localPath);

        string Utf8ToLocal(string utf8Path);
    }


    public class DefaultPathRepresentationConverter : IPathRepresentationConverter
    {
        public string LocalToUtf8(string localPath)
        {
            return localPath;
        }
        public string Utf8ToLocal(string utf8Path)
        {
            return utf8Path;
        }
    }

    /// <summary>
    /// Path representation converter.
    /// </summary>
    public static class PathRepresentationConverter
    {
        private static IPathRepresentationConverter PathConverter = new DefaultPathRepresentationConverter();

        static public void SetConverter(IPathRepresentationConverter converter)
        {
            PathConverter = converter;
        }

        static public string LocalToUtf8(string localPath)
        {
            return PathConverter.LocalToUtf8(localPath);
        }

        static public string Utf8ToLocal(string utf8Path)
        {
            return PathConverter.Utf8ToLocal(utf8Path);
        }
    }
}

