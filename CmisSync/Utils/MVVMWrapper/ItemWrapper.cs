using System;
using System.ComponentModel;

namespace CmisSync.Utils.MVVMWrapper {
   public interface IItemWrapper<TSource> {
       Boolean IsItemWrapper(TSource item);
   }
}
