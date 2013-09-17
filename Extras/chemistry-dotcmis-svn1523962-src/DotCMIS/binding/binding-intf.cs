/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using DotCMIS.Binding.Impl;
using DotCMIS.Binding.Services;
using DotCMIS.CMISWebServicesReference;
using System.Security.Principal;

namespace DotCMIS.Binding
{
    public interface ICmisBinding : IDisposable
    {
        IRepositoryService GetRepositoryService();
        INavigationService GetNavigationService();
        IObjectService GetObjectService();
        IVersioningService GetVersioningService();
        IRelationshipService GetRelationshipService();
        IDiscoveryService GetDiscoveryService();
        IMultiFilingService GetMultiFilingService();
        IAclService GetAclService();
        IPolicyService GetPolicyService();
        IAuthenticationProvider GetAuthenticationProvider();
        void ClearAllCaches();
        void ClearRepositoryCache(string repositoryId);
    }

    public interface IBindingSession
    {
        object GetValue(string key);
        object GetValue(string key, object defValue);
        int GetValue(string key, int defValue);
    }

    public interface IAuthenticationProvider
    {
        void Authenticate(object connection);

        void HandleResponse(object connection);
    }

    public abstract class AbstractAuthenticationProvider : IAuthenticationProvider
    {
        public IBindingSession Session { get; set; }
        public CookieContainer Cookies { get; set; }

        public abstract void Authenticate(object connection);

        public void HandleResponse(object connection)
        {
        }

        public string GetUser()
        {
            return Session.GetValue(SessionParameter.User) as string;
        }

        public string GetPassword()
        {
            return Session.GetValue(SessionParameter.Password) as string;
        }
    }

    public class StandardAuthenticationProvider : AbstractAuthenticationProvider
    {
        public StandardAuthenticationProvider()
        {
            Cookies = new CookieContainer();
        }

        public override void Authenticate(object connection)
        {
            HttpWebRequest request = connection as HttpWebRequest;
            if (request != null)
            {
                // AtomPub and browser binding authentictaion
                HttpAuthenticate(request);
            }
            else
            {
                // Web Service binding authentication
                WebServicesAuthenticate(connection);
            }
        }

        protected virtual void HttpAuthenticate(HttpWebRequest request)
        {
            string user = GetUser();
            string password = GetPassword();

            request.AllowWriteStreamBuffering = false;
            request.CookieContainer = Cookies;
            if (user != null || password != null)
            {
                if (request.Headers.GetValues("Authorization") == null)
                {
                    request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes((user ?? "") + ":" + (password ?? ""))));
                }
            }
        }

        protected virtual void WebServicesAuthenticate(object connection)
        {
            RepositoryServicePortClient repositoryServicePortClient = connection as RepositoryServicePortClient;
            if (repositoryServicePortClient != null)
            {
                AddWebServicesCredentials(repositoryServicePortClient.Endpoint, repositoryServicePortClient.ClientCredentials);
                return;
            }

            NavigationServicePortClient navigationServicePortClient = connection as NavigationServicePortClient;
            if (navigationServicePortClient != null)
            {
                AddWebServicesCredentials(navigationServicePortClient.Endpoint, navigationServicePortClient.ClientCredentials);
                return;
            }

            ObjectServicePortClient objectServicePortClient = connection as ObjectServicePortClient;
            if (objectServicePortClient != null)
            {
                AddWebServicesCredentials(objectServicePortClient.Endpoint, objectServicePortClient.ClientCredentials);
                return;
            }

            VersioningServicePortClient versioningServicePortClient = connection as VersioningServicePortClient;
            if (versioningServicePortClient != null)
            {
                AddWebServicesCredentials(versioningServicePortClient.Endpoint, versioningServicePortClient.ClientCredentials);
                return;
            }

            DiscoveryServicePortClient discoveryServicePortClient = connection as DiscoveryServicePortClient;
            if (discoveryServicePortClient != null)
            {
                AddWebServicesCredentials(discoveryServicePortClient.Endpoint, discoveryServicePortClient.ClientCredentials);
                return;
            }

            RelationshipServicePortClient relationshipServicePortClient = connection as RelationshipServicePortClient;
            if (relationshipServicePortClient != null)
            {
                AddWebServicesCredentials(relationshipServicePortClient.Endpoint, relationshipServicePortClient.ClientCredentials);
                return;
            }

            MultiFilingServicePortClient multiFilingServicePortClient = connection as MultiFilingServicePortClient;
            if (multiFilingServicePortClient != null)
            {
                AddWebServicesCredentials(multiFilingServicePortClient.Endpoint, multiFilingServicePortClient.ClientCredentials);
                return;
            }

            PolicyServicePortClient policyServicePortClient = connection as PolicyServicePortClient;
            if (policyServicePortClient != null)
            {
                AddWebServicesCredentials(policyServicePortClient.Endpoint, policyServicePortClient.ClientCredentials);
                return;
            }

            ACLServicePortClient aclServicePortClient = connection as ACLServicePortClient;
            if (aclServicePortClient != null)
            {
                AddWebServicesCredentials(aclServicePortClient.Endpoint, aclServicePortClient.ClientCredentials);
                return;
            }
        }

        protected virtual void AddWebServicesCredentials(ServiceEndpoint endpoint, ClientCredentials clientCredentials)
        {
            string user = GetUser();
            string password = GetPassword();

            if (user != null || password != null)
            {
                clientCredentials.UserName.UserName = user ?? "";
                clientCredentials.UserName.Password = password ?? "";
            }
            else
            {
                CustomBinding binding = endpoint.Binding as CustomBinding;
                if (binding != null)
                {
                    // remove SecurityBindingElement because neither a username nor a password have been set
                    binding.Elements.RemoveAll<SecurityBindingElement>();
                }
            }
        }
    }

    public class NtlmAuthenticationProvider : StandardAuthenticationProvider
    {
        public NtlmAuthenticationProvider()
        {
            Cookies = new CookieContainer();
        }

        protected override void HttpAuthenticate(HttpWebRequest request)
        {
            if (request != null)
            {
                string user = GetUser();
                string password = GetPassword();

                if ((user == string.Empty || user == null) && (password == string.Empty || password == null))
                {
                    request.Credentials = CredentialCache.DefaultNetworkCredentials;
                }
                else
                {
                    request.Credentials = new NetworkCredential(user, password);
                }
                
                request.CookieContainer = Cookies;
                request.AllowWriteStreamBuffering = true;
            }
        }

        protected override void AddWebServicesCredentials(ServiceEndpoint endpoint, ClientCredentials clientCredentials)
        {
            CustomBinding binding = endpoint.Binding as CustomBinding;
            if (binding != null)
            {
                // remove SecurityBindingElement
                binding.Elements.RemoveAll<SecurityBindingElement>();

                // add HTTP authentication
                HttpsTransportBindingElement htbe = binding.Elements.Find<HttpsTransportBindingElement>();
                htbe.AuthenticationScheme = AuthenticationSchemes.Negotiate;

                clientCredentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Delegation;

                string user = GetUser();
                string password = GetPassword();

                if ((user == string.Empty || user == null) && (password == string.Empty || password == null))
                {
                    clientCredentials.Windows.ClientCredential = CredentialCache.DefaultNetworkCredentials;
                }
                else
                {
                    clientCredentials.Windows.ClientCredential = new NetworkCredential(user, password);
                }
            }
        }
    }

    public class CmisBindingFactory
    {
        // Default CMIS AtomPub binding SPI implementation
        public const string BindingSpiAtomPub = "DotCMIS.Binding.AtomPub.CmisAtomPubSpi";
        // Default CMIS Web Services binding SPI implementation
        public const string BindingSpiWebServices = "DotCMIS.Binding.WebServices.CmisWebServicesSpi";

        public const string StandardAuthenticationProviderClass = "DotCMIS.Binding.StandardAuthenticationProvider";

        private IDictionary<string, string> defaults;

        private CmisBindingFactory()
        {
            defaults = CreateNewDefaultParameters();
        }

        public static CmisBindingFactory NewInstance()
        {
            return new CmisBindingFactory();
        }

        public IDictionary<string, string> GetDefaultSessionParameters()
        {
            return defaults;
        }

        public void SetDefaultSessionParameters(IDictionary<string, string> sessionParameters)
        {
            if (sessionParameters == null)
            {
                defaults = CreateNewDefaultParameters();
            }
            else
            {
                defaults = sessionParameters;
            }
        }

        public ICmisBinding CreateCmisBinding(IDictionary<string, string> sessionParameters, AbstractAuthenticationProvider authenticationProvider)
        {
            CheckSessionParameters(sessionParameters, true);
            AddDefaultParameters(sessionParameters);

            return new CmisBinding(sessionParameters, authenticationProvider);
        }

        public ICmisBinding CreateCmisAtomPubBinding(IDictionary<string, string> sessionParameters, AbstractAuthenticationProvider authenticationProvider)
        {
            CheckSessionParameters(sessionParameters, false);
            sessionParameters[SessionParameter.BindingSpiClass] = BindingSpiAtomPub;
            if (authenticationProvider == null)
            {
                if (!sessionParameters.ContainsKey(SessionParameter.AuthenticationProviderClass))
                {
                    sessionParameters[SessionParameter.AuthenticationProviderClass] = StandardAuthenticationProviderClass;
                }
            }

            AddDefaultParameters(sessionParameters);

            Check(sessionParameters, SessionParameter.AtomPubUrl);

            return new CmisBinding(sessionParameters, authenticationProvider);
        }

        public ICmisBinding CreateCmisWebServicesBinding(IDictionary<string, string> sessionParameters, AbstractAuthenticationProvider authenticationProvider)
        {
            CheckSessionParameters(sessionParameters, false);
            sessionParameters[SessionParameter.BindingSpiClass] = BindingSpiWebServices;
            if (authenticationProvider == null)
            {
                if (!sessionParameters.ContainsKey(SessionParameter.AuthenticationProviderClass))
                {
                    sessionParameters[SessionParameter.AuthenticationProviderClass] = StandardAuthenticationProviderClass;
                }
            }

            AddDefaultParameters(sessionParameters);

            Check(sessionParameters, SessionParameter.WebServicesAclService);
            Check(sessionParameters, SessionParameter.WebServicesDiscoveryService);
            Check(sessionParameters, SessionParameter.WebServicesMultifilingService);
            Check(sessionParameters, SessionParameter.WebServicesNavigationService);
            Check(sessionParameters, SessionParameter.WebServicesObjectService);
            Check(sessionParameters, SessionParameter.WebServicesPolicyService);
            Check(sessionParameters, SessionParameter.WebServicesRelationshipService);
            Check(sessionParameters, SessionParameter.WebServicesRepositoryService);
            Check(sessionParameters, SessionParameter.WebServicesVersioningService);

            return new CmisBinding(sessionParameters, authenticationProvider);
        }

        // ---- internals ----

        private void CheckSessionParameters(IDictionary<string, string> sessionParameters, bool mustContainSpi)
        {
            // don't accept null
            if (sessionParameters == null)
            {
                throw new ArgumentNullException("sessionParameters");
            }

            // check binding entry
            if (mustContainSpi)
            {
                string spiClass;

                if (sessionParameters.TryGetValue(SessionParameter.BindingSpiClass, out spiClass))
                {
                    throw new ArgumentException("SPI class entry (" + SessionParameter.BindingSpiClass + ") is missing!");
                }

                if ((spiClass == null) || (spiClass.Trim().Length == 0))
                {
                    throw new ArgumentException("SPI class entry (" + SessionParameter.BindingSpiClass + ") is invalid!");
                }
            }
        }

        private void Check(IDictionary<string, string> sessionParameters, String parameter)
        {
            if (!sessionParameters.ContainsKey(parameter))
            {
                throw new ArgumentException("Parameter '" + parameter + "' is missing!");
            }
        }

        private void AddDefaultParameters(IDictionary<string, string> sessionParameters)
        {
            foreach (string key in defaults.Keys)
            {
                if (!sessionParameters.ContainsKey(key))
                {
                    sessionParameters[key] = defaults[key];
                }
            }
        }

        private IDictionary<string, string> CreateNewDefaultParameters()
        {
            IDictionary<string, string> result = new Dictionary<string, string>();

            result[SessionParameter.CacheSizeRepositories] = "10";
            result[SessionParameter.CacheSizeTypes] = "100";
            result[SessionParameter.CacheSizeLinks] = "400";

            return result;
        }
    }
}
