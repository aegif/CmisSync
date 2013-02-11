using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SparkleLib.Cmis
{
    public class CmisPermissionDeniedException : Exception
    {
        public CmisPermissionDeniedException(string message) : base(message) { }
    }
}
