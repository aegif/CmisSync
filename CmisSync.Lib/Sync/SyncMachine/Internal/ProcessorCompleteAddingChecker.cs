using System;
namespace CmisSync.Lib.Sync.SyncMachine.Internal
{
    public class ProcessorCompleteAddingChecker
    {

        public ProcessorCompleteAddingChecker (ItemsDependencies _idps)
        {
            idps = _idps;
            assemblerCompleted = false;
        }

        public bool processorCompleteAdding()
        {
            return assemblerCompleted && dependeciesResolved ();
        }

        public bool assemblerCompleted { get; set; }

        private bool dependeciesResolved()
        {
            return idps.isAllResolved ();
        }

        private ItemsDependencies idps;
    }
}
