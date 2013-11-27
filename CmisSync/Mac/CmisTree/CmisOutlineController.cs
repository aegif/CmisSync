using System;
using System.Collections.Generic;
using System.Linq;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class CmisOutlineController : MonoMac.AppKit.NSViewController
    {
        protected NSOutlineViewDataSource DataSource = null;
        protected NSOutlineViewDelegate DataDelegate = null;
        #region Constructors

        // Called when created from unmanaged code
        public CmisOutlineController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public CmisOutlineController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public CmisOutlineController () : base ("CmisOutline", NSBundle.MainBundle)
        {
            Initialize ();
        }
        // Shared initialization code
        void Initialize ()
        {
        }
        public CmisOutlineController(NSOutlineViewDataSource dataSource,NSOutlineViewDelegate dataDelegate) : base ("CmisOutline", NSBundle.MainBundle)
        {
            DataSource = dataSource;
            DataDelegate = dataDelegate;
            Initialize ();
        }

        public override void AwakeFromNib ()
        {
            base.AwakeFromNib ();

            this.Outline.DataSource = DataSource;
            this.Outline.Delegate = DataDelegate;
        }

        #endregion

        public NSOutlineView OutlineView()
        {
            return this.Outline;
        }

        //strongly typed view accessor
        public new CmisOutline View {
            get {
                return (CmisOutline)base.View;
            }
        }
    }
}

