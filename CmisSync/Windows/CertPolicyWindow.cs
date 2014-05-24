using System;

using System.Windows;

using log4net;

namespace CmisSync
{
    class CertPolicyWindow
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
            var r = MessageBox.Show(Handler.UserMessage +
                "\n\n"+ Properties_Resources.DoYouTrustTheCertificate,
                Properties_Resources.UntrustedCertificate, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            switch (r) {
                case MessageBoxResult.Yes:
                    ret = CertPolicyHandler.Response.CertAcceptAlways;
                    break;
                case MessageBoxResult.No:
                    ret = CertPolicyHandler.Response.CertDeny;
                    break;
                case MessageBoxResult.Cancel:
                    ret = CertPolicyHandler.Response.CertAcceptSession;
                    break;
            }
            Logger.Debug("Cert Dialog return:" + ret.ToString());
            Handler.UserResponse = ret;
        }

    }
}

