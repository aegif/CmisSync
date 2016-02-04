using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace CmisSync.Auth.Specific.NemakiWare
{
    class NemakiAuthenticationProvider : DotCMIS.Binding.StandardAuthenticationProvider
    {
        protected override void HttpAuthenticate(HttpWebRequest request)
        {
            base.HttpAuthenticate(request);
            var s = Session;
            request.Headers["nemaki_auth_token"] = (String)Session.GetValue("nemaki_auth_token");
            request.Headers["nemaki_auth_token_app"] = (String)Session.GetValue("nemaki_auth_token_app");
        }
    }
}
