//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
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
using System.Drawing;
using System.IO;
using System.Globalization;

using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;
using MonoMac.WebKit;

using CmisSync.CmisTree;

namespace CmisSync {

    public class OutlineViewDelegate : NSOutlineViewDelegate {

        public delegate void NotificationDelegate (NSNotification notification);
        public event NotificationDelegate SelectionChanged = delegate {};
        public event NotificationDelegate ItemExpanded = delegate {};

        public override void SelectionDidChange (NSNotification notification)
        {
            SelectionChanged (notification);
        }

        public override void ItemDidExpand(NSNotification notification)
        {
            ItemExpanded (notification);
        }

        public override NSCell GetCell(NSOutlineView view, NSTableColumn column, MonoMac.Foundation.NSObject item)
        {
            NSCmisTree cmis = item as NSCmisTree;
            if (cmis == null) {
                Console.WriteLine ("GetCell Error");
                return null;
            }
            if (column == null) {
                return null;
            } else if (column.Identifier == "Name") {
//                Console.WriteLine ("GetCell " + cmis);
                NSButtonCell cell = new NSButtonCell ();
                cell.SetButtonType (NSButtonType.Switch);
                cell.AllowsMixedState = true;
                cell.Title = cmis.Name;
                cell.Editable = true;
                return cell;
            } else {
                NSTextFieldCell cell = new NSTextFieldCell ();
                return cell;
            }
        }
    }
}
