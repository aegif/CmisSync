//   CmisSync, a CMIS synchronization tool.
//   Copyright (C) 2012  Nicolas Raoul <nicolas.raoul@aegif.jp>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using DotCMIS.Data.Extensions;

using CmisSync.Lib;
using System.ComponentModel;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

using System.Data;
using System.Collections;

namespace CmisSync.Lib.Sync
{

    public partial class CmisRepo : RepoBase
    {
        private SynchronizedFolder cmis;

        public CmisRepo(RepoInfo repoInfo, ActivityListener activityListener)
            : base(repoInfo)
        {
            cmis = new SynchronizedFolder(repoInfo, activityListener, this);
            Logger.Info("CmisRepo | " + cmis);
        }


        public void DoFirstSync()
        {
            Logger.Info(String.Format("CmisRepo | First sync", this.Name));
            if (cmis != null)
                cmis.Sync();
        }


        public override void HasUnsyncedChanges()
        {
            Logger.Info(String.Format("CmisRepo | HasUnsyncedChanges get", this.Name));
            if (cmis != null) // Because it is sometimes called before the object's constructor has completed.
                cmis.SyncInBackground();
        }

        public override double Size
        {
            get
            {
                return 1234567; // TODO
            }
        }
    }
}
