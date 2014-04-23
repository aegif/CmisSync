using System;
using Gdk;
using Gtk;

namespace CmisSync.CmisTree
{
    /// <summary>
    /// LoadingStatus cell renderer.
    /// </summary>
    public class StatusCellRenderer : Gtk.CellRendererText
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CmisSync.CmisTree.StatusCellRenderer"/> class with the given foreground color
        /// </summary>
        /// <param name='foreground'>
        /// Foreground.
        /// </param>
        public StatusCellRenderer (Gdk.Color foreground) : base()
        {
            base.ForegroundGdk = foreground;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CmisSync.CmisTree.StatusCellRenderer"/> class with a default foreground color
        /// </summary>
        public StatusCellRenderer () : base()
        {
            Gdk.Color foreground = new Gdk.Color();
            /// Default color is a light gray
            Gdk.Color.Parse ("#999", ref foreground);
            base.ForegroundGdk = foreground;
        }
    }
}

