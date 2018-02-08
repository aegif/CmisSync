using System;

namespace CmisSync.Lib.Utilities.PathConverter
{
    /// <summary>
    /// Identity converter.
    /// </summary>
    public class DefaultPathRepresentationConverter : IPathRepresentationConverter
    {
        public string LocalToRemote (string localPath)
        {
            return localPath;
        }

        public string RemoteToLocal (string remotePath)
        {
            return remotePath;
        }
    }
}
