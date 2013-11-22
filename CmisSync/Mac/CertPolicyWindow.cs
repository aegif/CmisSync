using System;

using MonoMac.Foundation;
using MonoMac.AppKit;

using log4net;

namespace CmisSync
{
    class CertPolicyWindow : NSObject
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CertPolicyWindow));

        private CertPolicyHandler Handler { get; set; }

        public CertPolicyWindow (CertPolicyHandler handler)
        {
            Handler = handler;
            Handler.ShowWindowEvent += ShowCertDialog;
        }

        private void ShowCertDialog() {
            Logger.Debug("Showing Cert Dialog: " + Handler.UserMessage);
            CertPolicyHandler.Response ret = CertPolicyHandler.Response.None;
            InvokeOnMainThread(delegate {
                NSAlert alert = NSAlert.WithMessage("Untrusted Certificate", "No", "Always", "Just now", Handler.UserMessage + "\n\nDo you trust this certificate?");
                switch (alert.RunModal()) {
                case 1:
                    ret = CertPolicyHandler.Response.CertDeny;
                    break;
                case 0:
                    ret = CertPolicyHandler.Response.CertAcceptAlways;
                    break;
                case -1:
                    ret = CertPolicyHandler.Response.CertAcceptSession;
                    break;
                default:
                    ret = CertPolicyHandler.Response.None;
                    break;
                }
            });
            Logger.Debug("Cert Dialog return:" + ret.ToString());
            Handler.UserResponse = ret;
        }

    }
}

