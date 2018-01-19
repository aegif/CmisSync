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

    class YesCertPolicyHandler : ICertificatePolicy
    {
        /**
         * Verification callback.
         * Return true to accept a certificate.
         */
        public bool CheckValidationResult(ServicePoint sp, X509Certificate certificate,
                WebRequest request, int error)
        {
            // Always accept.
            // Note: Command-line users often have self-made certificates, and know what they are doing.
            return true;
        }
    }
}