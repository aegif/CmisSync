using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Events
{
    public class ConfigChangedEvent : ISyncEvent
    {
        public override string ToString()
        {
            return "ConfigChangedEvent";
        }
    }

    public class RepoConfigChangedEvent : ConfigChangedEvent
    {
        public readonly RepoInfo RepoInfo;
        public RepoConfigChangedEvent(RepoInfo repoInfo) {
            RepoInfo = repoInfo;
        }

        public override string ToString()
        {
            return String.Format("RepoConfigChangedEvent: {0}", RepoInfo.Name);
        }
    }
}
