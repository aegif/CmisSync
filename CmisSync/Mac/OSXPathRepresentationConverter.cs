using System;
using System.Text;
using MonoMac.Foundation;
using CmisSync.Lib;

namespace CmisSync
{
    public class OSXPathRepresentationConverter : IPathRepresentationConverter
    {
    
        public string LocalToUtf8(string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                return localPath;
            }

            return localPath.Normalize(NormalizationForm.FormC);
        }

        public string Utf8ToLocal(string utf8Path)
        {
            if (String.IsNullOrEmpty(utf8Path))
            {
                return utf8Path;
            }

            var url = NSUrl.FromFilename(utf8Path);
            if (utf8Path.StartsWith("/"))
            {
                return url.Path;
            }

            return url.Path.Substring(1);
        }
    }
}

