using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Threading;
using MonoMac.Foundation;
using MonoMac.AppKit;

using CmisSync.Auth;
using CmisSync.Lib.Cmis;

namespace CmisSync
{
    public partial class SetupSubLoginController : MonoMac.AppKit.NSViewController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupSubLoginController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupSubLoginController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupSubLoginController (SetupController controller) : base ("SetupSubLogin", NSBundle.MainBundle)
        {
            this.Controller = controller;
            Initialize ();
        }
        // Shared initialization code
        void Initialize ()
        {
        }

        #endregion

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);
            Console.WriteLine (this.GetType ().ToString () + " disposed " + disposing.ToString ());
        }

        public override void AwakeFromNib ()
        {
            base.AwakeFromNib ();

            this.AddressLabel.StringValue = Properties_Resources.EnterWebAddress;
            this.UserLabel.StringValue = Properties_Resources.User + ": ";
            this.PasswordLabel.StringValue = Properties_Resources.Password + ": ";

            this.AddressDelegate = new TextFieldDelegate();
            this.AddressText.Delegate = this.AddressDelegate;
            this.UserDelegate = new TextFieldDelegate();
            this.UserText.Delegate = this.UserDelegate;
            this.PasswordDelegate = new TextFieldDelegate();
            this.PasswordText.Delegate = this.PasswordDelegate;

            this.ContinueButton.Title = Properties_Resources.Continue;
            this.CancelButton.Title = Properties_Resources.Cancel;

            this.AddressText.StringValue = (Controller.PreviousAddress == null || String.IsNullOrEmpty (Controller.PreviousAddress.ToString ())) ? "https://" : Controller.PreviousAddress.ToString ();
            this.UserText.StringValue = String.IsNullOrEmpty (Controller.saved_user) ? Environment.UserName : Controller.saved_user;
            //            this.PasswordText.StringValue = String.IsNullOrEmpty (Controller.saved_password) ? "" : Controller.saved_password;
            this.PasswordText.StringValue = "";


            // Cmis server address help link
            string helpLabel = Properties_Resources.Help + ": ";
            string helpLink = Properties_Resources.WhereToFind;
            string addressUrl = @"https://github.com/aegif/CmisSync/wiki/What-address";
            this.AddressHelp.AllowsEditingTextAttributes = true;
            this.AddressHelp.Selectable = true;           
            var attrStr = new NSMutableAttributedString(helpLabel + helpLink);
            var labelRange = new NSRange(0, helpLabel.Length);
            var linkRange = new NSRange(helpLabel.Length, helpLink.Length);
            var url = new NSUrl(addressUrl);
            var font = this.AddressHelp.Font;
            var paragraph = new NSMutableParagraphStyle()
            {
                LineBreakMode = this.AddressHelp.Cell.LineBreakMode,
                Alignment = this.AddressHelp.Alignment
            };
            attrStr.BeginEditing();
            attrStr.AddAttribute(NSAttributedString.LinkAttributeName, url, linkRange);
            attrStr.AddAttribute(NSAttributedString.ForegroundColorAttributeName, NSColor.Blue, linkRange);
            attrStr.AddAttribute(NSAttributedString.ForegroundColorAttributeName, NSColor.Gray, labelRange);
            attrStr.AddAttribute(NSAttributedString.UnderlineStyleAttributeName, new NSNumber(1), linkRange);
            attrStr.AddAttribute(NSAttributedString.FontAttributeName, font, new NSRange(0, attrStr.Length));
            attrStr.AddAttribute(NSAttributedString.ParagraphStyleAttributeName, paragraph, new NSRange(0, attrStr.Length));
            attrStr.EndEditing();
            this.AddressHelp.AttributedStringValue = attrStr;

            InsertEvent ();

            //  Must be called after InsertEvent()
            CheckTextFields ();
        }

        void InsertEvent()
        {
            this.AddressDelegate.StringValueChanged += CheckTextFields;
            this.UserDelegate.StringValueChanged += CheckTextFields;
            this.PasswordDelegate.StringValueChanged += CheckTextFields;
            Controller.UpdateSetupContinueButtonEvent += SetContinueButton;
            Controller.UpdateAddProjectButtonEvent += SetContinueButton;
        }

        void RemoveEvent()
        {
            this.AddressDelegate.StringValueChanged -= CheckTextFields;
            this.UserDelegate.StringValueChanged -= CheckTextFields;
            this.PasswordDelegate.StringValueChanged -= CheckTextFields;
            Controller.UpdateSetupContinueButtonEvent -= SetContinueButton;
            Controller.UpdateAddProjectButtonEvent -= SetContinueButton;
        }

        void SetContinueButton(bool enabled)
        {
            InvokeOnMainThread(delegate
            {
                if (!enabled || string.IsNullOrEmpty(this.UserText.StringValue) || string.IsNullOrEmpty(this.PasswordText.StringValue))
                {
                    ContinueButton.Enabled = false;
                }
                else
                {
                    ContinueButton.Enabled = true;
                    //                ContinueButton.KeyEquivalent = "\r";
                }
            });

        }

        void CheckTextFields()
        {
            InvokeOnMainThread (delegate
            {
                string error = Controller.CheckAddPage (AddressText.StringValue);
                if (String.IsNullOrEmpty (error))
                    WarnText.StringValue = "";
                else
                    WarnText.StringValue = Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentUICulture);
            });
        }

        SetupController Controller;
        TextFieldDelegate AddressDelegate;
        TextFieldDelegate UserDelegate;
        TextFieldDelegate PasswordDelegate;

        partial void OnCancel (MonoMac.Foundation.NSObject sender)
        {
            RemoveEvent();
            Controller.PageCancelled();
        }

        partial void OnContinue (MonoMac.Foundation.NSObject sender)
        {
            ServerCredentials credentials = new ServerCredentials() {
                UserName = UserText.StringValue,
                Password = PasswordText.StringValue,
                Address = new Uri(AddressText.StringValue)
            };
            AddressText.Enabled = false;
            UserText.Enabled = false;
            PasswordText.Enabled = false;
            ContinueButton.Enabled = false;
            CancelButton.Enabled = false;

            Thread check = new Thread(() => {
                Tuple<CmisServer, Exception> fuzzyResult = CmisUtils.GetRepositoriesFuzzy(credentials);
                CmisServer cmisServer = fuzzyResult.Item1;
                if (cmisServer != null)
                {
                    Controller.repositories = cmisServer.Repositories;
                }
                else
                {
                    Controller.repositories = null;
                    // Could not retrieve repositories list from server
                    string warning = "";
                }
                InvokeOnMainThread(delegate {
                    if (Controller.repositories == null)
                    {
                        // WarnText.StringValue = Controller.getConnectionsProblemWarning(fuzzyResult.Item1, fuzzyResult.Item2);
                        string message = fuzzyResult.Item2.Message;
                        string warning = "";
                        Exception e = fuzzyResult.Item2;
                        if (e is PermissionDeniedException)
                        {
                            warning = Properties_Resources.LoginFailedForbidden;
                        }
                        else if (e is ServerNotFoundException)
                        {
                            warning = Properties_Resources.ConnectFailure;
                        }
                        else if (e.Message == "SendFailure" && cmisServer.Url.Scheme.StartsWith("https"))
                        {
                            warning = Properties_Resources.SendFailureHttps;
                        }
                        else if (e.Message == "TrustFailure")
                        {
                            warning = Properties_Resources.TrustFailure;
                        }
                        else if (e.Message == "Unauthorized")
                        {
                            warning = Properties_Resources.LoginFailedForbidden;
                        }
                        else
                        {
                            warning = message + Environment.NewLine + Properties_Resources.Sorry;
                        }

                        WarnText.StringValue = warning;
                        AddressText.Enabled = true;
                        UserText.Enabled = true;
                        PasswordText.Enabled = true;
                        ContinueButton.Enabled = true;
                        CancelButton.Enabled = true;
                        // TODO remove  this line for debug

                    }
                    else
                    {
                        RemoveEvent();
                        Controller.Add1PageCompleted(cmisServer.Url, credentials.UserName, credentials.Password.ToString());
                    }
                });
            });
            check.Start();
        }


        //strongly typed view accessor
        public new SetupSubLogin View {
            get {
                return (SetupSubLogin)base.View;
            }
        }
    }
}

