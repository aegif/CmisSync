using System;

namespace CmisSync.Lib.Utilities.PathConverter
{
    /// <summary>
    /// Converter between local path representation to remote path representation.
    /// Example:
    ///  - Remote: aproject/adir/a&lt;file
    ///  - Local: A Project\adir\afile.txt
    /// </summary>    
    public interface IPathRepresentationConverter
    {
        string LocalToRemote (string localPath);

        string RemoteToLocal (string remotePath);
    }
}
