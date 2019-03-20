using System;
using System.Collections.Generic;
using CmisSync.Lib.Sync.SyncTriplet;

namespace CmisSync.Lib.Sync.SyncMachine.Internal
{
    public class ReverseLexicoGraphicalComparer<T> : IComparer<T>
    {

        public int Compare (T a, T b)
        {
            return 0 - Helper (a.ToString(), b.ToString());
        }

        private int Helper (string a, string b)
        {
            /*
             * C# will split "/" by '/' to an array with 2 elements: "", '/', ""
             * will split "/a/b/c/" by '/' to an array with 5 elements: "", "a", "b", "c", ""
             * so "" does nothing to the algoritm
             */            
            String [] m = a.Split ('/');
            String [] n = b.Split ('/');
            int l = Math.Min (m.Length, n.Length);
            int i = 0;
            while (i < l) {
                if (string.Compare (m [i], n [i]) < 0) return -1;
                if (string.Compare (m [i], n [i]) > 0) return 1;
                i++;
            }
            if (m.Length > n.Length) return 1;
            if (m.Length < n.Length) return -1;
            return 0;
        }
    }
}
