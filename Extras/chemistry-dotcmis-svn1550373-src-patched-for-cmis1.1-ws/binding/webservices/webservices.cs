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
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using DotCMIS.Binding.Impl;
using DotCMIS.Binding.Services;
using DotCMIS.CMISWebServicesReference;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace DotCMIS.Binding.WebServices
{
    /// <summary>
    /// Web Services binding SPI.
    /// </summary>
    internal class CmisWebServicesSpi : ICmisSpi
    {
        private RepositoryService repositoryService;
        private NavigationService navigationService;
        private ObjectService objectService;
        private VersioningService versioningService;
        private DiscoveryService discoveryService;
        private MultiFilingService multiFilingService;
        private RelationshipService relationshipService;
        private PolicyService policyService;
        private AclService aclService;

        public void initialize(BindingSession session)
        {
            PortProvider provider = new PortProvider(session);

            repositoryService = new RepositoryService(session, provider);
            navigationService = new NavigationService(session, provider);
            objectService = new ObjectService(session, provider);
            versioningService = new VersioningService(session, provider);
            discoveryService = new DiscoveryService(session, provider);
            multiFilingService = new MultiFilingService(session, provider);
            relationshipService = new RelationshipService(session, provider);
            policyService = new PolicyService(session, provider);
            aclService = new AclService(session, provider);
        }

        public IRepositoryService GetRepositoryService()
        {
            return repositoryService;
        }

        public INavigationService GetNavigationService()
        {
            return navigationService;
        }

        public IObjectService GetObjectService()
        {
            return objectService;
        }

        public IVersioningService GetVersioningService()
        {
            return versioningService;
        }

        public IRelationshipService GetRelationshipService()
        {
            return relationshipService;
        }

        public IDiscoveryService GetDiscoveryService()
        {
            return discoveryService;
        }

        public IMultiFilingService GetMultiFilingService()
        {
            return multiFilingService;
        }

        public IAclService GetAclService()
        {
            return aclService;
        }

        public IPolicyService GetPolicyService()
        {
            return policyService;
        }

        public void ClearAllCaches()
        {
            // nothing to do
        }

        public void ClearRepositoryCache(string repositoryId)
        {
            // nothing to do
        }

        public void Dispose()
        {
            // nothing to do
        }
    }

    internal class PortProvider
    {
        [ThreadStatic]
        private static IDictionary<int, IDictionary<string, object>> Services;

        private BindingSession session;

        public PortProvider(BindingSession session)
        {
            this.session = session;
        }

        private static IDictionary<string, object> GetServiceDictionary(BindingSession session)
        {
            if (Services == null)
            {
                Services = new Dictionary<int, IDictionary<string, object>>();
            }

            IDictionary<string, object> serviceDict;
            if (Services.TryGetValue(session.GetHashCode(), out serviceDict))
            {
                return serviceDict;
            }

            serviceDict = new Dictionary<string, object>();
            Services[session.GetHashCode()] = serviceDict;

            return serviceDict;
        }

        public RepositoryServicePortClient GetRepositoryServicePort()
        {
            return (RepositoryServicePortClient)GetPortObject(SessionParameter.WebServicesRepositoryService);
        }

        public NavigationServicePortClient GetNavigationService()
        {
            return (NavigationServicePortClient)GetPortObject(SessionParameter.WebServicesNavigationService);
        }

        public ObjectServicePortClient GetObjectService()
        {
            return (ObjectServicePortClient)GetPortObject(SessionParameter.WebServicesObjectService);
        }

        public VersioningServicePortClient GetVersioningService()
        {
            return (VersioningServicePortClient)GetPortObject(SessionParameter.WebServicesVersioningService);
        }

        public DiscoveryServicePortClient GetDiscoveryService()
        {
            return (DiscoveryServicePortClient)GetPortObject(SessionParameter.WebServicesDiscoveryService);
        }

        public MultiFilingServicePortClient GetMultiFilingService()
        {
            return (MultiFilingServicePortClient)GetPortObject(SessionParameter.WebServicesMultifilingService);
        }

        public RelationshipServicePortClient GetRelationshipService()
        {
            return (RelationshipServicePortClient)GetPortObject(SessionParameter.WebServicesRelationshipService);
        }

        public PolicyServicePortClient GetPolicyService()
        {
            return (PolicyServicePortClient)GetPortObject(SessionParameter.WebServicesPolicyService);
        }

        public ACLServicePortClient GetAclService()
        {
            return (ACLServicePortClient)GetPortObject(SessionParameter.WebServicesAclService);
        }

        private object GetPortObject(string serviceKey)
        {
            IDictionary<string, object> servicesDict = GetServiceDictionary(session);

            object portObject;
            if (!servicesDict.TryGetValue(serviceKey, out portObject))
            {
                portObject = InitServiceObject(serviceKey);
                servicesDict[serviceKey] = portObject;
            }

            return portObject;
        }

        private object InitServiceObject(string serviceKey)
        {
            object portObject = null;

            CustomBinding binding;

            string wcfBinding = session.GetValue(SessionParameter.WebServicesWCFBinding) as string;

            if (wcfBinding != null)
            {
                binding = new CustomBinding(wcfBinding);
            }
            else
            {
                long messageSize = session.GetValue(SessionParameter.MessageSize, 4 * 1024 * 1024);

                List<BindingElement> elements = new List<BindingElement>();

                SecurityBindingElement securityElement = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
                securityElement.SecurityHeaderLayout = SecurityHeaderLayout.LaxTimestampFirst;

                string enableUnsecuredResponseFlag = session.GetValue(SessionParameter.WebServicesEnableUnsecuredResponse) as string;
                if (enableUnsecuredResponseFlag != null && enableUnsecuredResponseFlag.ToLower().Equals("true"))
                {
                    PropertyInfo eur = securityElement.GetType().GetProperty("EnableUnsecuredResponse");
                    if (eur != null)
                    {
                        eur.GetSetMethod().Invoke(securityElement, new object[] { true });
                    }
                }

                elements.Add(securityElement);

                MtomMessageEncodingBindingElement mtomElement = new MtomMessageEncodingBindingElement();
                mtomElement.MessageVersion = MessageVersion.Soap11;
                mtomElement.MaxBufferSize = (messageSize > Int32.MaxValue ? Int32.MaxValue : (int)messageSize);
                elements.Add(mtomElement);

                HttpsTransportBindingElement transportElement = new HttpsTransportBindingElement();
                transportElement.MaxReceivedMessageSize = messageSize;
                transportElement.TransferMode = TransferMode.Streamed;
                transportElement.AllowCookies = true;
                elements.Add(transportElement);

                binding = new CustomBinding(elements);
                TimeSpan timeout;

                string openTimeOut = session.GetValue(SessionParameter.WebServicesOpenTimeout) as string;
                if (openTimeOut != null && TimeSpan.TryParse(openTimeOut, out timeout))
                {
                    binding.OpenTimeout = timeout;
                }

                string closeTimeOut = session.GetValue(SessionParameter.WebServicesCloseTimeout) as string;
                if (closeTimeOut != null && TimeSpan.TryParse(closeTimeOut, out timeout))
                {
                    binding.CloseTimeout = timeout;
                }

                string sendTimeOut = session.GetValue(SessionParameter.WebServicesSendTimeout) as string;
                if (sendTimeOut != null && TimeSpan.TryParse(sendTimeOut, out timeout))
                {
                    binding.SendTimeout = timeout;
                }

                string receiveTimeOut = session.GetValue(SessionParameter.WebServicesReceiveTimeout) as string;
                if (receiveTimeOut != null && TimeSpan.TryParse(receiveTimeOut, out timeout))
                {
                    binding.ReceiveTimeout = timeout;
                }
            }

            if (serviceKey == SessionParameter.WebServicesRepositoryService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesRepositoryService) as string;
                portObject = new RepositoryServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }
            else if (serviceKey == SessionParameter.WebServicesNavigationService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesNavigationService) as string;
                portObject = new NavigationServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }
            else if (serviceKey == SessionParameter.WebServicesObjectService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesObjectService) as string;
                portObject = new ObjectServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }
            else if (serviceKey == SessionParameter.WebServicesVersioningService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesVersioningService) as string;
                portObject = new VersioningServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }
            else if (serviceKey == SessionParameter.WebServicesDiscoveryService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesDiscoveryService) as string;
                portObject = new DiscoveryServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }
            else if (serviceKey == SessionParameter.WebServicesRelationshipService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesRelationshipService) as string;
                portObject = new RelationshipServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }
            else if (serviceKey == SessionParameter.WebServicesMultifilingService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesMultifilingService) as string;
                portObject = new MultiFilingServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }
            else if (serviceKey == SessionParameter.WebServicesPolicyService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesPolicyService) as string;
                portObject = new PolicyServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }
            else if (serviceKey == SessionParameter.WebServicesAclService)
            {
                string wsdlUrl = session.GetValue(SessionParameter.WebServicesAclService) as string;
                portObject = new ACLServicePortClient(binding, new EndpointAddress(wsdlUrl));
            }

            IAuthenticationProvider authenticationProvider = session.GetAuthenticationProvider();
            if (authenticationProvider != null)
            {
                authenticationProvider.Authenticate(portObject);
            }

            return portObject;
        }
    }

    /// <summary>
    /// Common service methods.
    /// </summary>
    internal abstract class AbstractWebServicesService
    {
        protected BindingSession Session { get; set; }

        protected PortProvider Provider { get; set; }

        protected CmisBaseException ConvertException(FaultException<cmisFaultType> fault)
        {
            if ((fault == null) || (fault.Detail == null))
            {
                return new CmisRuntimeException("CmisException has no fault!");
            }

            String msg = fault.Detail.message;
            long? code = null;

            try
            {
                code = Int64.Parse(fault.Detail.code);
            }
            catch (Exception)
            {
                // ignore
            }

            switch (fault.Detail.type)
            {
                case enumServiceException.constraint:
                    return new CmisConstraintException(msg, code);
                case enumServiceException.contentAlreadyExists:
                    return new CmisContentAlreadyExistsException(msg, code);
                case enumServiceException.filterNotValid:
                    return new CmisFilterNotValidException(msg, code);
                case enumServiceException.invalidArgument:
                    return new CmisInvalidArgumentException(msg, code);
                case enumServiceException.nameConstraintViolation:
                    return new CmisNameConstraintViolationException(msg, code);
                case enumServiceException.notSupported:
                    return new CmisNotSupportedException(msg, code);
                case enumServiceException.objectNotFound:
                    return new CmisObjectNotFoundException(msg, code);
                case enumServiceException.permissionDenied:
                    return new CmisPermissionDeniedException(msg, code);
                case enumServiceException.runtime:
                    return new CmisRuntimeException(msg, code);
                case enumServiceException.storage:
                    return new CmisStorageException(msg, code);
                case enumServiceException.streamNotSupported:
                    return new CmisStreamNotSupportedException(msg, code);
                case enumServiceException.updateConflict:
                    return new CmisUpdateConflictException(msg, code);
                case enumServiceException.versioning:
                    return new CmisVersioningException(msg, code);
            }

            return new CmisRuntimeException("Unknown exception[" + fault.Detail.type + "]: " + msg);
        }
    }

    internal class RepositoryService : AbstractWebServicesService, IRepositoryService
    {
        public RepositoryService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public IList<IRepositoryInfo> GetRepositoryInfos(IExtensionsData extension)
        {
            RepositoryServicePortClient port = Provider.GetRepositoryServicePort();

            try
            {
                cmisRepositoryEntryType[] entries = port.getRepositories(Converter.ConvertExtension(extension));

                if (entries == null)
                {
                    return null;
                }

                IList<IRepositoryInfo> result = new List<IRepositoryInfo>();
                foreach (cmisRepositoryEntryType entry in entries)
                {
                    cmisRepositoryInfoType info = port.getRepositoryInfo(entry.repositoryId, null);
                    result.Add(Converter.Convert(info));
                }

                return result;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IRepositoryInfo GetRepositoryInfo(string repositoryId, IExtensionsData extension)
        {
            RepositoryServicePortClient port = Provider.GetRepositoryServicePort();

            try
            {
                return Converter.Convert(port.getRepositoryInfo(repositoryId, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public ITypeDefinitionList GetTypeChildren(string repositoryId, string typeId, bool? includePropertyDefinitions,
                long? maxItems, long? skipCount, IExtensionsData extension)
        {
            RepositoryServicePortClient port = Provider.GetRepositoryServicePort();

            try
            {
                return Converter.Convert(port.getTypeChildren(repositoryId, typeId, includePropertyDefinitions,
                    maxItems.ToString(), skipCount.ToString(), Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IList<ITypeDefinitionContainer> GetTypeDescendants(string repositoryId, string typeId, long? depth,
                bool? includePropertyDefinitions, IExtensionsData extension)
        {
            RepositoryServicePortClient port = Provider.GetRepositoryServicePort();

            try
            {
                cmisTypeContainer[] descendants = port.getTypeDescendants(
                    repositoryId, typeId, depth.ToString(), includePropertyDefinitions, Converter.ConvertExtension(extension));

                if (descendants == null)
                {
                    return null;
                }

                List<ITypeDefinitionContainer> result = new List<ITypeDefinitionContainer>();
                foreach (cmisTypeContainer container in descendants)
                {
                    result.Add(Converter.Convert(container));
                }

                return result;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public ITypeDefinition GetTypeDefinition(string repositoryId, string typeId, IExtensionsData extension)
        {
            RepositoryServicePortClient port = Provider.GetRepositoryServicePort();

            try
            {
                return Converter.Convert(port.getTypeDefinition(repositoryId, typeId, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }

    internal class NavigationService : AbstractWebServicesService, INavigationService
    {
        public NavigationService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public IObjectInFolderList GetChildren(string repositoryId, string folderId, string filter, string orderBy,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, long? maxItems, long? skipCount, IExtensionsData extension)
        {
            NavigationServicePortClient port = Provider.GetNavigationService();

            try
            {
                return Converter.Convert(port.getChildren(repositoryId, folderId, filter, orderBy, includeAllowableActions,
                    (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships), renditionFilter,
                    includePathSegment, maxItems.ToString(), skipCount.ToString(), Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IList<IObjectInFolderContainer> GetDescendants(string repositoryId, string folderId, long? depth, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, IExtensionsData extension)
        {
            NavigationServicePortClient port = Provider.GetNavigationService();

            try
            {
                cmisObjectInFolderContainerType[] descendants = port.getDescendants(repositoryId, folderId, depth.ToString(), filter,
                    includeAllowableActions, (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships),
                    renditionFilter, includePathSegment, Converter.ConvertExtension(extension));

                if (descendants == null)
                {
                    return null;
                }

                List<IObjectInFolderContainer> result = new List<IObjectInFolderContainer>();
                foreach (cmisObjectInFolderContainerType container in descendants)
                {
                    result.Add(Converter.Convert(container));
                }

                return result;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IList<IObjectInFolderContainer> GetFolderTree(string repositoryId, string folderId, long? depth, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, IExtensionsData extension)
        {
            NavigationServicePortClient port = Provider.GetNavigationService();

            try
            {
                cmisObjectInFolderContainerType[] descendants = port.getFolderTree(repositoryId, folderId, depth.ToString(), filter,
                    includeAllowableActions, (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships),
                    renditionFilter, includePathSegment, Converter.ConvertExtension(extension));

                if (descendants == null)
                {
                    return null;
                }

                List<IObjectInFolderContainer> result = new List<IObjectInFolderContainer>();
                foreach (cmisObjectInFolderContainerType container in descendants)
                {
                    result.Add(Converter.Convert(container));
                }

                return result;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IList<IObjectParentData> GetObjectParents(string repositoryId, string objectId, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includeRelativePathSegment, IExtensionsData extension)
        {
            NavigationServicePortClient port = Provider.GetNavigationService();

            try
            {
                cmisObjectParentsType[] parents = port.getObjectParents(repositoryId, objectId, filter,
                    includeAllowableActions, (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships),
                    renditionFilter, includeRelativePathSegment, Converter.ConvertExtension(extension));

                if (parents == null)
                {
                    return null;
                }

                List<IObjectParentData> result = new List<IObjectParentData>();
                foreach (cmisObjectParentsType parent in parents)
                {
                    result.Add(Converter.Convert(parent));
                }

                return result;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IObjectData GetFolderParent(string repositoryId, string folderId, string filter, ExtensionsData extension)
        {
            NavigationServicePortClient port = Provider.GetNavigationService();

            try
            {
                return Converter.Convert(port.getFolderParent(repositoryId, folderId, filter, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IObjectList GetCheckedOutDocs(string repositoryId, string folderId, string filter, string orderBy,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            NavigationServicePortClient port = Provider.GetNavigationService();

            try
            {
                return Converter.Convert(port.getCheckedOutDocs(repositoryId, folderId, filter, orderBy, includeAllowableActions,
                    (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships), renditionFilter,
                    maxItems.ToString(), skipCount.ToString(), Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }

    internal class ObjectService : AbstractWebServicesService, IObjectService
    {
        public ObjectService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public string CreateDocument(string repositoryId, IProperties properties, string folderId, IContentStream contentStream,
            VersioningState? versioningState, IList<string> policies, IAcl addAces, IAcl removeAces, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                string objectId = port.createDocument(repositoryId, Converter.Convert(properties), folderId, Converter.Convert(contentStream),
                    (enumVersioningState?)CmisValue.CmisToSerializerEnum(versioningState), Converter.ConvertList(policies),
                    Converter.Convert(addAces), Converter.Convert(removeAces), ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);

                return objectId;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public string CreateDocumentFromSource(string repositoryId, string sourceId, IProperties properties, string folderId,
            VersioningState? versioningState, IList<string> policies, IAcl addAces, IAcl removeAces, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                string objectId = port.createDocumentFromSource(repositoryId, sourceId, Converter.Convert(properties), folderId,
                    (enumVersioningState?)CmisValue.CmisToSerializerEnum(versioningState), Converter.ConvertList(policies),
                    Converter.Convert(addAces), Converter.Convert(removeAces), ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);

                return objectId;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public string CreateFolder(string repositoryId, IProperties properties, string folderId, IList<string> policies,
            IAcl addAces, IAcl removeAces, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                string objectId = port.createFolder(repositoryId, Converter.Convert(properties), folderId,
                    Converter.ConvertList(policies), Converter.Convert(addAces), Converter.Convert(removeAces), ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);

                return objectId;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public string CreateRelationship(string repositoryId, IProperties properties, IList<string> policies, IAcl addAces,
            IAcl removeAces, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                string objectId = port.createRelationship(repositoryId, Converter.Convert(properties), Converter.ConvertList(policies),
                    Converter.Convert(addAces), Converter.Convert(removeAces), ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);

                return objectId;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }

        }

        public string CreatePolicy(string repositoryId, IProperties properties, string folderId, IList<string> policies,
            IAcl addAces, IAcl removeAces, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                string objectId = port.createPolicy(repositoryId, Converter.Convert(properties), folderId,
                    Converter.ConvertList(policies), Converter.Convert(addAces), Converter.Convert(removeAces), ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);

                return objectId;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IAllowableActions GetAllowableActions(string repositoryId, string objectId, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                return Converter.Convert(port.getAllowableActions(repositoryId, objectId, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IProperties GetProperties(string repositoryId, string objectId, string filter, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                return Converter.Convert(port.getProperties(repositoryId, objectId, filter, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IList<IRenditionData> GetRenditions(string repositoryId, string objectId, string renditionFilter,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisRenditionType[] renditions = port.getRenditions(repositoryId, objectId, renditionFilter,
                    maxItems.ToString(), skipCount.ToString(), Converter.ConvertExtension(extension));

                if (renditions == null)
                {
                    return null;
                }

                IList<IRenditionData> result = new List<IRenditionData>();
                foreach (cmisRenditionType rendition in renditions)
                {
                    result.Add(Converter.Convert(rendition));
                }

                return result;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IObjectData GetObject(string repositoryId, string objectId, string filter, bool? includeAllowableActions,
            IncludeRelationshipsFlag? includeRelationships, string renditionFilter, bool? includePolicyIds,
            bool? includeAcl, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                return Converter.Convert(port.getObject(repositoryId, objectId, filter, includeAllowableActions,
                    (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships), renditionFilter,
                    includePolicyIds, includeAcl, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IObjectData GetObjectByPath(string repositoryId, string path, string filter, bool? includeAllowableActions,
            IncludeRelationshipsFlag? includeRelationships, string renditionFilter, bool? includePolicyIds, bool? includeAcl,
            IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                return Converter.Convert(port.getObjectByPath(repositoryId, path, filter, includeAllowableActions,
                    (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships), renditionFilter,
                    includePolicyIds, includeAcl, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IContentStream GetContentStream(string repositoryId, string objectId, string streamId, long? offset, long? length,
            IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                return Converter.Convert(port.getContentStream(
                    repositoryId, objectId, streamId, offset.ToString(), length.ToString(), Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void UpdateProperties(string repositoryId, ref string objectId, ref string changeToken, IProperties properties,
            IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.updateProperties(repositoryId, ref objectId, ref changeToken, Converter.Convert(properties), ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void MoveObject(string repositoryId, ref string objectId, string targetFolderId, string sourceFolderId,
            IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.moveObject(repositoryId, ref objectId, targetFolderId, sourceFolderId, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void DeleteObject(string repositoryId, string objectId, bool? allVersions, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.deleteObject(repositoryId, objectId, allVersions, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IFailedToDeleteData DeleteTree(string repositoryId, string folderId, bool? allVersions, UnfileObject? unfileObjects,
            bool? continueOnFailure, ExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                return Converter.Convert(port.deleteTree(repositoryId, folderId, allVersions,
                    (enumUnfileObject?)CmisValue.CmisToSerializerEnum(unfileObjects), continueOnFailure,
                    Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void SetContentStream(string repositoryId, ref string objectId, bool? overwriteFlag, ref string changeToken,
            IContentStream contentStream, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.setContentStream(repositoryId, ref objectId, overwriteFlag, ref changeToken,
                    Converter.Convert(contentStream), ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void DeleteContentStream(string repositoryId, ref string objectId, ref string changeToken, IExtensionsData extension)
        {
            ObjectServicePortClient port = Provider.GetObjectService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.deleteContentStream(repositoryId, ref objectId, ref changeToken, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }

    internal class VersioningService : AbstractWebServicesService, IVersioningService
    {
        public VersioningService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public void CheckOut(string repositoryId, ref string objectId, IExtensionsData extension, out bool? contentCopied)
        {
            VersioningServicePortClient port = Provider.GetVersioningService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                contentCopied = port.checkOut(repositoryId, ref objectId, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void CancelCheckOut(string repositoryId, string objectId, IExtensionsData extension)
        {
            VersioningServicePortClient port = Provider.GetVersioningService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.cancelCheckOut(repositoryId, objectId, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void CheckIn(string repositoryId, ref string objectId, bool? major, IProperties properties,
            IContentStream contentStream, string checkinComment, IList<string> policies, IAcl addAces, IAcl removeAces,
            IExtensionsData extension)
        {
            VersioningServicePortClient port = Provider.GetVersioningService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.checkIn(repositoryId, ref objectId, major, Converter.Convert(properties), Converter.Convert(contentStream),
                    checkinComment, Converter.ConvertList(policies), Converter.Convert(addAces), Converter.Convert(removeAces),
                    ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IObjectData GetObjectOfLatestVersion(string repositoryId, string objectId, string versionSeriesId, bool major,
            string filter, bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships,
            string renditionFilter, bool? includePolicyIds, bool? includeAcl, IExtensionsData extension)
        {
            VersioningServicePortClient port = Provider.GetVersioningService();

            try
            {
                return Converter.Convert(port.getObjectOfLatestVersion(repositoryId, versionSeriesId, major, filter,
                    includeAllowableActions, (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships),
                    renditionFilter, includePolicyIds, includeAcl, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IProperties GetPropertiesOfLatestVersion(string repositoryId, string objectId, string versionSeriesId, bool major,
            string filter, IExtensionsData extension)
        {
            VersioningServicePortClient port = Provider.GetVersioningService();

            try
            {
                return Converter.Convert(port.getPropertiesOfLatestVersion(repositoryId, versionSeriesId, major, filter,
                    Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IList<IObjectData> GetAllVersions(string repositoryId, string objectId, string versionSeriesId, string filter,
            bool? includeAllowableActions, IExtensionsData extension)
        {
            VersioningServicePortClient port = Provider.GetVersioningService();

            try
            {
                cmisObjectType[] versions = port.getAllVersions(repositoryId, versionSeriesId, filter, includeAllowableActions,
                    Converter.ConvertExtension(extension));

                if (versions == null)
                {
                    return null;
                }

                IList<IObjectData> result = new List<IObjectData>();
                foreach (cmisObjectType version in versions)
                {
                    result.Add(Converter.Convert(version));
                }

                return result;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }

    internal class RelationshipService : AbstractWebServicesService, IRelationshipService
    {
        public RelationshipService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public IObjectList GetObjectRelationships(string repositoryId, string objectId, bool? includeSubRelationshipTypes,
            RelationshipDirection? relationshipDirection, string typeId, string filter, bool? includeAllowableActions,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            RelationshipServicePortClient port = Provider.GetRelationshipService();

            try
            {
                return Converter.Convert(port.getObjectRelationships(repositoryId, objectId,
                    includeSubRelationshipTypes == null ? true : (bool)includeSubRelationshipTypes,
                    (enumRelationshipDirection?)CmisValue.CmisToSerializerEnum(relationshipDirection), typeId, filter, includeAllowableActions,
                    maxItems.ToString(), skipCount.ToString(), Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }

    internal class DiscoveryService : AbstractWebServicesService, IDiscoveryService
    {
        public DiscoveryService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public IObjectList Query(string repositoryId, string statement, bool? searchAllVersions,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            DiscoveryServicePortClient port = Provider.GetDiscoveryService();

            try
            {
                return Converter.Convert(port.query(repositoryId, statement, searchAllVersions, includeAllowableActions,
                    (enumIncludeRelationships?)CmisValue.CmisToSerializerEnum(includeRelationships), renditionFilter,
                    maxItems.ToString(), skipCount.ToString(), Converter.ConvertExtension(extension), null));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IObjectList GetContentChanges(string repositoryId, ref string changeLogToken, bool? includeProperties,
           string filter, bool? includePolicyIds, bool? includeAcl, long? maxItems, IExtensionsData extension)
        {
            DiscoveryServicePortClient port = Provider.GetDiscoveryService();

            try
            {
                return Converter.Convert(port.getContentChanges(repositoryId, ref changeLogToken, includeProperties, filter,
                    includePolicyIds, includeAcl, maxItems.ToString(), Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }

    internal class MultiFilingService : AbstractWebServicesService, IMultiFilingService
    {
        public MultiFilingService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public void AddObjectToFolder(string repositoryId, string objectId, string folderId, bool? allVersions, IExtensionsData extension)
        {
            MultiFilingServicePortClient port = Provider.GetMultiFilingService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.addObjectToFolder(repositoryId, objectId, folderId, allVersions == null ? true : (bool)allVersions, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void RemoveObjectFromFolder(string repositoryId, string objectId, string folderId, IExtensionsData extension)
        {
            MultiFilingServicePortClient port = Provider.GetMultiFilingService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.removeObjectFromFolder(repositoryId, objectId, folderId, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }

    internal class AclService : AbstractWebServicesService, IAclService
    {
        public AclService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public IAcl GetAcl(string repositoryId, string objectId, bool? onlyBasicPermissions, IExtensionsData extension)
        {
            ACLServicePortClient port = Provider.GetAclService();

            try
            {
                return Converter.Convert(port.getACL(repositoryId, objectId, onlyBasicPermissions, Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IAcl ApplyAcl(string repositoryId, string objectId, IAcl addAces, IAcl removeAces, AclPropagation? aclPropagation,
            IExtensionsData extension)
        {
            ACLServicePortClient port = Provider.GetAclService();

            try
            {
                return Converter.Convert(port.applyACL(repositoryId, objectId, Converter.Convert(addAces), Converter.Convert(removeAces),
                    (enumACLPropagation?)CmisValue.CmisToSerializerEnum(aclPropagation), Converter.ConvertExtension(extension)));
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }

    internal class PolicyService : AbstractWebServicesService, IPolicyService
    {
        public PolicyService(BindingSession session, PortProvider provider)
        {
            Session = session;
            Provider = provider;
        }

        public void ApplyPolicy(string repositoryId, string policyId, string objectId, IExtensionsData extension)
        {
            PolicyServicePortClient port = Provider.GetPolicyService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.applyPolicy(repositoryId, policyId, objectId, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public void RemovePolicy(string repositoryId, string policyId, string objectId, IExtensionsData extension)
        {
            PolicyServicePortClient port = Provider.GetPolicyService();

            try
            {
                cmisExtensionType cmisExtension = Converter.ConvertExtension(extension);

                port.removePolicy(repositoryId, policyId, objectId, ref cmisExtension);

                Converter.ConvertExtension(cmisExtension, extension);
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }

        public IList<IObjectData> GetAppliedPolicies(string repositoryId, string objectId, string filter, IExtensionsData extension)
        {
            PolicyServicePortClient port = Provider.GetPolicyService();

            try
            {
                cmisObjectType[] policies = port.getAppliedPolicies(repositoryId, objectId, filter, Converter.ConvertExtension(extension));

                if (policies == null)
                {
                    return null;
                }

                List<IObjectData> result = new List<IObjectData>();
                foreach (cmisObjectType policy in policies)
                {
                    result.Add(Converter.Convert(policy));
                }

                return result;
            }
            catch (FaultException<cmisFaultType> fe)
            {
                throw ConvertException(fe);
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException("Error: " + e.Message, e);
            }
        }
    }
}
