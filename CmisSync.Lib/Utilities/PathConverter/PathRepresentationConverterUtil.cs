using System;

namespace CmisSync.Lib.Utilities.PathConverter
{
    /// <summary>Path representation converter.</summary>
    public static class PathRepresentationConverterUtil
    {
        private static IPathRepresentationConverter PathConverter = new DefaultPathRepresentationConverter ();

        static public void SetConverter (IPathRepresentationConverter converter)
        {
            PathConverter = converter;
        }

        static public string LocalToRemote (string localPath)
        {
            return PathConverter.LocalToRemote (localPath);
        }

        static public string RemoteToLocal (string remotePath)
        {
            return PathConverter.RemoteToLocal (remotePath);
        }
    }
}
