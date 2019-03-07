using System;
namespace CmisSync.Lib.Sync.SyncMachine.Internal
{
    public class ProcessorCompleteAddingChecker
    {

        public ProcessorCompleteAddingChecker (ItemsDependencies _idps)
        {
            idps = _idps;

        }

        public bool processorCompleteAdding()
        {
            return assemblerCompleted && dependeciesResolved ();
        }

        public bool assemblerCompleted { get; set; } = true;

        private bool dependeciesResolved()
        {
            return idps.isAllResolved ();
        }

        private ItemsDependencies idps;
    }
}
