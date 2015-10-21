using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmisSync.Lib.Utils
{
    public class LimitedQueue<T> : Queue<T>
    {
        public int MaxItemsCount { get; set; }
        private int _dequeuedItems;

        public LimitedQueue(int maxItemsCount)
        {
            this.MaxItemsCount = maxItemsCount;
        }

        public new void Enqueue(T obj)
        {
            while (this.Count >= this.MaxItemsCount) // If would work just as well as while here
            {
                _dequeuedItems++;
                this.Dequeue();
            }
            base.Enqueue(obj);
        }

        /// <summary>
        /// return the number of Dequeued items due to size limit and reset the counter
        /// </summary>
        /// <returns></returns>
        public int DequeuedItemsDueToLimit()
        {
            int val = this._dequeuedItems;
            this._dequeuedItems = 0;
            return  val;
        }
    }
}
