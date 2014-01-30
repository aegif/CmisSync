using System;
using System.Threading;
using Gtk;

using log4net;

namespace CmisSync
{
    class CertPolicyWindow
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof(CertPolicyWindow));

        private CertPolicyHandler Handler { get; set; }

        public CertPolicyWindow (CertPolicyHandler handler)
        {
            Handler = handler;
            Handler.ShowWindowEvent += ShowCertDialog;
        }

        private void ShowCertDialog ()
        {
            Logger.Debug ("Showing Cert Dialog: " + Handler.UserMessage);
            CertPolicyHandler.Response ret = CertPolicyHandler.Response.None;
            using (var handle = new AutoResetEvent(false)) {
                Application.Invoke (delegate {
                    try {
                        using (MessageDialog md = new MessageDialog (null, DialogFlags.Modal,
                        MessageType.Warning, ButtonsType.None, Handler.UserMessage +
                        "\n\nDo you trust this certificate?") {
                            Title = "Untrusted Certificate"})
                        {
                            using (var noButton = md.AddButton("No", (int)CertPolicyHandler.Response.CertDeny))
                            using (var justNowButton = md.AddButton("Just now", (int)CertPolicyHandler.Response.CertAcceptSession))
                            using (var alwaysButton = md.AddButton("Always", (int)CertPolicyHandler.Response.CertAcceptAlways))
                            {
                                ret = (CertPolicyHandler.Response)md.Run ();
                                md.Destroy ();
                            }
                        }
                    } finally {
                        handle.Set ();
                    }
                }
                );
                handle.WaitOne ();
            }
            Logger.Debug ("Cert Dialog return:" + ret.ToString ());
            Handler.UserResponse = ret;
        }

    }
}

