using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    class Objects
    {
        public static int GetHashCode(object obj)
        {
            return obj != null?obj.GetHashCode():0;
        }
    }
}
