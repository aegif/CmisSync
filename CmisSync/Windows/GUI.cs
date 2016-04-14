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
using System.Windows.Forms;

namespace CmisSync {

    /// <summary>
    /// User interface of CmisSync.
    /// </summary>
    public class GUI {

        /// <summary>
        /// Dialog shown at first run to explain how CmisSync works.
        /// </summary>
        public Setup Setup;


        /// <summary>
        /// CmisSync icon in the task bar.
        /// It contains the main CmisSync menu.
        /// </summary>
        public StatusIcon StatusIcon;


        /// <summary>
        /// Small dialog showing some information about CmisSync.
        /// </summary>
        public About About;


        /// <summary>
        /// Constructor.
        /// </summary>
        public GUI ()
        {   
			// TODO: The second time windows are shown, the windows
			// don't have the smooth ease in animation, but appear abruptly.
			// The ease out animation always seems to work
            Setup      = new Setup ();
            About      = new About ();
            
            Program.Controller.UIHasLoaded ();
        }

        
        /// <summary>
        /// Run the CmisSync user interface.
        /// </summary>
        public void Run ()
        {
            Application.Run (StatusIcon = new StatusIcon ());
            StatusIcon.Dispose ();
        }
    }
}
