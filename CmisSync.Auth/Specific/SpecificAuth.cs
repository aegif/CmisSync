using DotCMIS;
using DotCMIS.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Deserializers;
using Newtonsoft.Json.Linq;

namespace CmisSync.Auth.Specific
{
    class SpecificAuth
    {
        public static void setCmisParameters(Dictionary<string, string> parameters)
        {
            //Get token
            string user = parameters[SessionParameter.User];
            string password = parameters[SessionParameter.Password];
            string repositoryId = parameters[SessionParameter.RepositoryId];
            string atompubUrl = parameters[SessionParameter.AtomPubUrl];

            string token = CmisSync.Auth.Specific.NemakiWare.AuthTokenManager.getOrRegister(parameters);

            parameters[SessionParameter.AuthenticationProviderClass] = typeof(CmisSync.Auth.Specific.NemakiWare.NemakiAuthenticationProvider).AssemblyQualifiedName;
            parameters["nemaki_auth_token"] = token;
            parameters["nemaki_auth_token_app"] = "cmissync";
        }
    }
}
