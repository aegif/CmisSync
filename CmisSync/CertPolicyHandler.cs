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
using System.Net;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace CmisSync
{
    /**
     * Certificate policy handler.
     * This handler gets invoked when an untrusted certificate is encountered
     * while connecting via https.
     * It Presents the user with a dialog, asking if the cert should be trusted.
     */

    class CertPolicyHandler : ICertificatePolicy
    {
        private enum CertificateProblem : long {
            CertEXPIRED                   = 0x800B0101,
            CertVALIDITYPERIODNESTING     = 0x800B0102,
            CertROLE                      = 0x800B0103,
            CertPATHLENCONST              = 0x800B0104,
            CertCRITICAL                  = 0x800B0105,
            CertPURPOSE                   = 0x800B0106,
            CertISSUERCHAINING            = 0x800B0107,
            CertMALFORMED                 = 0x800B0108,
            CertUNTRUSTEDROOT             = 0x800B0109,
            CertCHAINING                  = 0x800B010A,
            CertREVOKED                   = 0x800B010C,
            CertUNTRUSTEDTESTROOT         = 0x800B010D,
            CertREVOCATION_FAILURE        = 0x800B010E,
            CertCN_NO_MATCH               = 0x800B010F,
            CertWRONG_USAGE               = 0x800B0110,
            CertUNTRUSTEDCA               = 0x800B0112
        }

        public CertPolicyHandler()
        {
            Window = new CertPolicyWindow (this);
        }

        private CertPolicyWindow Window { get; set; }

        //===== Actions =====
        /// <summary>
        /// Show User Interaction Action
        /// </summary>
        public event Action ShowWindowEvent = delegate { };

        /// <summary>
        /// Show User Interaction Window
        /// </summary>
        public void ShowWindow()
        {
            ShowWindowEvent();
        }

        /**
         * The user interaction method must return these values.
         */
        public enum Response : int {
            None,
            CertDeny,
            CertAcceptSession,
            CertAcceptAlways
        }

        /// <summary>
        /// Gets the message to display for user.
        /// </summary>
        public string UserMessage { get; private set; }
        /// <summary>
        /// Gets or sets the user response.
        /// </summary>
        public Response UserResponse { get; set; }

        /// <summary>
        /// List of temporarily accepted certs.
        /// </summary>
        private static List<X509Certificate2> acceptedCerts = new List<X509Certificate2>();
        /// <summary>
        /// List of already denied certs. Just for this session.
        /// </summary>
        private static List<X509Certificate2> deniedCerts = new List<X509Certificate2>();
        /// <summary>
        /// Persistent store of saved certificates
        /// </summary>
        private static X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

        /**
         * Verification callback.
         * Return true to accept a certificate.
         */
        public bool CheckValidationResult (ServicePoint sp, X509Certificate certificate,
                WebRequest request, int error)
        {
            if (0 == error)
            {
                return true;
            }
            X509Certificate2 cert = new X509Certificate2(certificate);
            {
                store.Open(OpenFlags.ReadOnly);
                bool found = store.Certificates.Contains(cert);
                store.Close();
                // If the certificate has been stored persistent, accept it
                if (found) return true;
            }
            if (acceptedCerts.Contains(cert))
            {
                // User has already accepted this certificate in this session.
                return true;
            }

            if (deniedCerts.Contains(cert))
            {
                // User has already denied this certificate in this session.
                return false;
            }

            UserMessage = GetCertificateHR(certificate) +
                GetProblemMessage((CertificateProblem)error);
            ShowWindow ();
            switch (UserResponse)
            {
                case Response.CertDeny:
                    // Deny this cert for the actual session
                    deniedCerts.Add(cert);
                    return false;
                case Response.CertAcceptSession:
                    // Just accept this cert for the actual session
                    acceptedCerts.Add(cert);
                    return true;
                case Response.CertAcceptAlways:
                    // Write the newly accepted cert to the persistent store
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(cert);
                    store.Close();
                    return true;
            }

            return false;
        }

        private String GetProblemMessage(CertificateProblem Problem)
        {
            String ProblemMessage = "";
            CertificateProblem problemList = new CertificateProblem();
            String ProblemCodeName = Enum.GetName(problemList.GetType(), Problem);
            if (null != ProblemCodeName) {
                ProblemMessage = ProblemMessage + "-Certificateproblem:" +
                    ProblemCodeName;
            } else {
                ProblemMessage = "Unknown Certificate Problem";
            }
            return ProblemMessage;
        }

        /**
         * Return a human readable string, describing the general Cert properties.
         */
        private String GetCertificateHR(X509Certificate x509) {
            X509Certificate2 x509_2 = new X509Certificate2(x509);
            bool selfsigned = (x509_2.IssuerName == x509_2.SubjectName);
            String ret = String.Format("{0}X.509 v{1} Certificate\n",
                    (selfsigned ? "Self-signed " : String.Empty), x509_2.Version);
            ret += String.Format("  Serial Number: {0}\n", x509_2.SerialNumber);
            ret += String.Format("  Issuer Name:   {0}\n", x509.Issuer);
            ret += String.Format("  Subject Name:  {0}\n", x509.Subject);
            ret += String.Format("  Valid From:    {0}\n", x509_2.NotBefore);
            ret += String.Format("  Valid Until:   {0}\n", x509_2.NotAfter);
            ret += String.Format("  Unique Hash:   {0}\n", x509.GetCertHashString());
            return ret;
        }
    }

}
