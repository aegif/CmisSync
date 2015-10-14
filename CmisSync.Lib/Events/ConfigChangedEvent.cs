using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CmisSync.Lib;

namespace CmisSync.Lib.Events
{
    /// <summary></summary>
    public class ConfigChangedEvent : ISyncEvent
    {
        /// <summary></summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "ConfigChangedEvent";
        }
    }

    /// <summary></summary>
    public class RepoConfigChangedEvent : ConfigChangedEvent
    {
        /// <summary></summary>
        public readonly Config.SyncConfig.SyncFolder RepoInfo;

        /// <summary></summary>
        /// <param name="repoInfo"></param>
        public RepoConfigChangedEvent(Config.SyncConfig.SyncFolder repoInfo)
        {
            RepoInfo = repoInfo;
        }

        /// <summary></summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("RepoConfigChangedEvent: {0}", RepoInfo.DisplayName);
        }
    }
}
