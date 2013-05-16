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
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using DotCMIS.CMISWebServicesReference;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace DotCMIS.Binding
{
    internal class Converter
    {
        private const string CMISNamespaceURI = "http://docs.oasis-open.org/ns/cmis/core/200908/";

        private delegate void ProcessAnyElement(XmlElement element);

        /// <summary>
        /// Walks through CMIS Any elements.
        /// </summary>
        private static void ProcessAnyElements(XmlElement[] any, ProcessAnyElement proc)
        {
            try
            {
                if (any != null)
                {
                    foreach (XmlElement element in any)
                    {
                        if (element.NamespaceURI.Equals(CMISNamespaceURI) && element.FirstChild != null)
                        {
                            proc(element);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new CmisRuntimeException(e.Message, e);
            }
        }

        /// <summary>
        /// Deserializes a string.
        /// </summary>
        private static string DeserializeString(XmlElement element)
        {
            return element.FirstChild.Value;
        }

        /// <summary>
        /// Deserializes an integer.
        /// </summary>
        private static long? DeserializeInteger(XmlElement element)
        {
            string s = DeserializeString(element);
            return s == null ? (long?)null : Int64.Parse(s);
        }

        /// <summary>
        /// Deserializes a decimal.
        /// </summary>
        private static decimal? DeserializeDecimal(XmlElement element)
        {
            string s = DeserializeString(element);
            return s == null ? (decimal?)null : Decimal.Parse(s);
        }

        /// <summary>
        /// Deserializes a boolean.
        /// </summary>
        private static Boolean DeserializeBoolean(XmlElement element)
        {
            return "true".Equals(DeserializeString(element), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Deserializes an enum.
        /// </summary>
        private static T DeserializeEnum<T>(XmlElement element)
        {
            return CmisValue.GetCmisEnum<T>(DeserializeString(element));
        }

        /// <summary>
        /// Deserializes an elemenet.
        /// </summary>
        private static T DeserializeElement<T>(XmlElement element)
        {
            XmlRootAttribute xmlRoot = new XmlRootAttribute(element.LocalName);
            xmlRoot.Namespace = CMISNamespaceURI;
            XmlSerializer s = new XmlSerializer(typeof(T), xmlRoot);
            return (T)s.Deserialize(new XmlNodeReader(element));
        }

        /// <summary>
        /// Converts a repository info.
        /// </summary>
        public static IRepositoryInfo Convert(cmisRepositoryInfoType repositotyInfo)
        {
            if (repositotyInfo == null)
            {
                return null;
            }

            RepositoryInfo result = new RepositoryInfo();
            result.Id = repositotyInfo.repositoryId;
            result.Name = repositotyInfo.repositoryName;
            result.Description = repositotyInfo.repositoryDescription;
            result.VendorName = repositotyInfo.vendorName;
            result.ProductName = repositotyInfo.productName;
            result.ProductVersion = repositotyInfo.productVersion;
            result.RootFolderId = repositotyInfo.rootFolderId;
            result.Capabilities = Convert(repositotyInfo.capabilities);
            result.AclCapabilities = Convert(repositotyInfo.aclCapability);
            result.LatestChangeLogToken = repositotyInfo.latestChangeLogToken;
            result.CmisVersionSupported = repositotyInfo.cmisVersionSupported;
            result.ThinClientUri = repositotyInfo.thinClientURI;
            result.ChangesIncomplete = (repositotyInfo.changesIncompleteSpecified ? (bool?)repositotyInfo.changesIncomplete : null);
            result.ChangesOnType = new List<BaseTypeId?>();
            if (repositotyInfo.changesOnType != null)
            {
                foreach (enumBaseObjectTypeIds baseType in repositotyInfo.changesOnType)
                {
                    result.ChangesOnType.Add((BaseTypeId?)CmisValue.SerializerToCmisEnum(baseType));
                }
            }
            result.PrincipalIdAnonymous = repositotyInfo.principalAnonymous;
            result.PrincipalIdAnyone = repositotyInfo.principalAnyone;

            ConvertExtension(repositotyInfo, result);

            return result;
        }

        /// <summary>
        /// Converts repository capabilities.
        /// </summary>
        public static IRepositoryCapabilities Convert(cmisRepositoryCapabilitiesType capabilities)
        {
            if (capabilities == null)
            {
                return null;
            }

            RepositoryCapabilities result = new RepositoryCapabilities();
            result.ContentStreamUpdatesCapability = (CapabilityContentStreamUpdates)CmisValue.SerializerToCmisEnum(capabilities.capabilityContentStreamUpdatability);
            result.ChangesCapability = (CapabilityChanges)CmisValue.SerializerToCmisEnum(capabilities.capabilityChanges);
            result.RenditionsCapability = (CapabilityRenditions)CmisValue.SerializerToCmisEnum(capabilities.capabilityRenditions);
            result.IsGetDescendantsSupported = capabilities.capabilityGetDescendants;
            result.IsGetFolderTreeSupported = capabilities.capabilityGetFolderTree;
            result.IsMultifilingSupported = capabilities.capabilityMultifiling;
            result.IsUnfilingSupported = capabilities.capabilityUnfiling;
            result.IsVersionSpecificFilingSupported = capabilities.capabilityVersionSpecificFiling;
            result.IsPwcSearchableSupported = capabilities.capabilityPWCSearchable;
            result.IsPwcUpdatableSupported = capabilities.capabilityPWCUpdatable;
            result.IsAllVersionsSearchableSupported = capabilities.capabilityAllVersionsSearchable;
            result.QueryCapability = (CapabilityQuery)CmisValue.SerializerToCmisEnum(capabilities.capabilityQuery);
            result.JoinCapability = (CapabilityJoin)CmisValue.SerializerToCmisEnum(capabilities.capabilityJoin);
            result.AclCapability = (CapabilityAcl)CmisValue.SerializerToCmisEnum(capabilities.capabilityACL);

            ConvertExtension(capabilities, result);

            return result;
        }

        /// <summary>
        /// Converts ACL capabilities.
        /// </summary>
        public static IAclCapabilities Convert(cmisACLCapabilityType capabilities)
        {
            if (capabilities == null)
            {
                return null;
            }

            AclCapabilities result = new AclCapabilities();
            result.SupportedPermissions = (SupportedPermissions)CmisValue.SerializerToCmisEnum(capabilities.supportedPermissions);
            result.AclPropagation = (AclPropagation)CmisValue.SerializerToCmisEnum(capabilities.propagation);
            result.Permissions = new List<IPermissionDefinition>();
            if (capabilities.permissions != null)
            {
                foreach (cmisPermissionDefinition permission in capabilities.permissions)
                {
                    PermissionDefinition permDef = new PermissionDefinition();
                    permDef.Id = permission.permission;
                    permDef.Description = permission.description;
                    result.Permissions.Add(permDef);

                    ConvertExtension(permission, permDef);
                }
            }
            result.PermissionMapping = new Dictionary<string, IPermissionMapping>();
            if (capabilities.mapping != null)
            {
                foreach (cmisPermissionMapping mapping in capabilities.mapping)
                {
                    PermissionMapping permMap = new PermissionMapping();
                    permMap.Key = GetXmlEnumAttributeValue(mapping.key);
                    permMap.Permissions = new List<string>();
                    if (mapping.permission != null)
                    {
                        foreach (string s in mapping.permission)
                        {
                            permMap.Permissions.Add(s);
                        }
                    }

                    result.PermissionMapping[permMap.Key] = permMap;

                    ConvertExtension(mapping, permMap);
                }
            }

            ConvertExtension(capabilities, result);

            return result;
        }

        public static ITypeDefinition Convert(cmisTypeDefinitionType typeDef)
        {
            if (typeDef == null)
            {
                return null;
            }

            AbstractTypeDefinition result = null;
            if (typeDef is cmisTypeDocumentDefinitionType)
            {
                DocumentTypeDefinition docType = new DocumentTypeDefinition();
                cmisTypeDocumentDefinitionType docTypeDef = (cmisTypeDocumentDefinitionType)typeDef;

                docType.IsVersionable = docTypeDef.versionable;
                docType.ContentStreamAllowed = (ContentStreamAllowed)CmisValue.SerializerToCmisEnum(docTypeDef.contentStreamAllowed);

                ProcessAnyElements(docTypeDef.Any, (element) =>
                {
                    if (element.LocalName.Equals("versionable"))
                    {
                        docType.IsVersionable = DeserializeBoolean(element);
                    }
                    else if (element.LocalName.Equals("contentStreamAllowed"))
                    {
                        docType.ContentStreamAllowed = DeserializeEnum<ContentStreamAllowed>(element);
                    }
                });

                result = docType;
            }
            else if (typeDef is cmisTypeFolderDefinitionType)
            {
                result = new FolderTypeDefinition();
            }
            else if (typeDef is cmisTypePolicyDefinitionType)
            {
                result = new PolicyTypeDefinition();
            }
            else if (typeDef is cmisTypeRelationshipDefinitionType)
            {
                RelationshipTypeDefinition relType = new RelationshipTypeDefinition();
                cmisTypeRelationshipDefinitionType relTypeDef = (cmisTypeRelationshipDefinitionType)typeDef;

                if (relTypeDef.allowedSourceTypes != null)
                {
                    relType.AllowedSourceTypeIds = new List<string>();
                    foreach (string id in relTypeDef.allowedSourceTypes)
                    {
                        relType.AllowedSourceTypeIds.Add(id);
                    }
                }

                if (relTypeDef.allowedTargetTypes != null)
                {
                    relType.AllowedTargetTypeIds = new List<string>();
                    foreach (string id in relTypeDef.allowedTargetTypes)
                    {
                        relType.AllowedTargetTypeIds.Add(id);
                    }
                }

                ProcessAnyElements(relTypeDef.Any, (element) =>
                {
                    if (element.LocalName.Equals("allowedSourceTypes"))
                    {
                        if (relType.AllowedSourceTypeIds == null)
                        {
                            relType.AllowedSourceTypeIds = new List<string>();
                        }
                        relType.AllowedSourceTypeIds.Add(element.FirstChild.Value);
                    }
                    else if (element.LocalName.Equals("allowedTargetTypes"))
                    {
                        if (relType.AllowedTargetTypeIds == null)
                        {
                            relType.AllowedTargetTypeIds = new List<string>();
                        }
                        relType.AllowedTargetTypeIds.Add(element.FirstChild.Value);
                    }
                });

                result = relType;
            }

            result.Id = typeDef.id;
            result.LocalName = typeDef.localName;
            result.LocalNamespace = typeDef.localNamespace;
            result.DisplayName = typeDef.displayName;
            result.QueryName = typeDef.queryName;
            result.Description = typeDef.description;
            result.BaseTypeId = (BaseTypeId)CmisValue.SerializerToCmisEnum(typeDef.baseId);
            result.ParentTypeId = typeDef.parentId;
            result.IsCreatable = typeDef.creatable;
            result.IsFileable = typeDef.fileable;
            result.IsQueryable = typeDef.queryable;
            result.IsFulltextIndexed = typeDef.fulltextIndexed;
            result.IsIncludedInSupertypeQuery = typeDef.includedInSupertypeQuery;
            result.IsControllablePolicy = typeDef.controllablePolicy;
            result.IsControllableAcl = typeDef.controllableACL;

            if (typeDef.Items != null)
            {
                foreach (cmisPropertyDefinitionType propertyDefinition in typeDef.Items)
                {
                    result.AddPropertyDefinition(Convert(propertyDefinition));
                }
            }

            ConvertExtension(typeDef, result);

            return result;
        }

        public static IPropertyDefinition Convert(cmisPropertyDefinitionType propDef)
        {
            if (propDef == null) { return null; }

            PropertyDefinition result = null;
            if (propDef is cmisPropertyBooleanDefinitionType)
            {
                PropertyBooleanDefinition pd = new PropertyBooleanDefinition();
                cmisPropertyBooleanDefinitionType cpd = (cmisPropertyBooleanDefinitionType)propDef;

                if (cpd.defaultValue != null && cpd.defaultValue.value != null)
                {
                    pd.DefaultValue = new List<bool>();
                    foreach (bool value in cpd.defaultValue.value) { pd.DefaultValue.Add(value); }
                }
                if (cpd.choice != null)
                {
                    pd.Choices = new List<IChoice<bool>>();
                    foreach (cmisChoiceBoolean c in cpd.choice) { pd.Choices.Add(ConvertChoice(c)); }
                }

                ProcessAnyElements(cpd.Any, (element) =>
                {
                    if (element.LocalName.Equals("defaultValue"))
                    {
                        cmisPropertyBoolean prop = DeserializeElement<cmisPropertyBoolean>(element);
                        if (prop != null && prop.value != null)
                        {
                            pd.DefaultValue = new List<bool>();
                            foreach (bool value in prop.value) { pd.DefaultValue.Add(value); }
                        }
                    }
                    else if (element.LocalName.Equals("choice"))
                    {
                        cmisChoiceBoolean choice = DeserializeElement<cmisChoiceBoolean>(element);
                        if (choice != null)
                        {
                            if (pd.Choices == null) { pd.Choices = new List<IChoice<bool>>(); }
                            pd.Choices.Add(ConvertChoice(choice));
                        }
                    }
                });

                result = pd;
            }
            else if (propDef is cmisPropertyDateTimeDefinitionType)
            {
                PropertyDateTimeDefinition pd = new PropertyDateTimeDefinition();
                cmisPropertyDateTimeDefinitionType cpd = (cmisPropertyDateTimeDefinitionType)propDef;

                if (cpd.defaultValue != null && cpd.defaultValue.value != null)
                {
                    pd.DefaultValue = new List<DateTime>();
                    foreach (DateTime value in cpd.defaultValue.value) { pd.DefaultValue.Add(value); }
                }
                if (cpd.choice != null)
                {
                    pd.Choices = new List<IChoice<DateTime>>();
                    foreach (cmisChoiceDateTime c in cpd.choice) { pd.Choices.Add(ConvertChoice(c)); }
                }

                if (cpd.resolutionSpecified)
                {
                    pd.DateTimeResolution = (DateTimeResolution)CmisValue.SerializerToCmisEnum(cpd.resolution);
                }

                ProcessAnyElements(cpd.Any, (element) =>
                {
                    if (element.LocalName.Equals("defaultValue"))
                    {
                        cmisChoiceDateTime prop = DeserializeElement<cmisChoiceDateTime>(element);
                        if (prop != null && prop.value != null)
                        {
                            pd.DefaultValue = new List<DateTime>();
                            foreach (DateTime value in prop.value) { pd.DefaultValue.Add(value); }
                        }
                    }
                    else if (element.LocalName.Equals("choice"))
                    {
                        cmisChoiceDateTime choice = DeserializeElement<cmisChoiceDateTime>(element);
                        if (choice != null)
                        {
                            if (pd.Choices == null) { pd.Choices = new List<IChoice<DateTime>>(); }
                            pd.Choices.Add(ConvertChoice(choice));
                        }
                    }
                    else if (element.LocalName.Equals("maxLength"))
                    {
                        pd.DateTimeResolution = DeserializeEnum<DateTimeResolution>(element);
                    }
                });

                result = pd;
            }
            else if (propDef is cmisPropertyDecimalDefinitionType)
            {
                PropertyDecimalDefinition pd = new PropertyDecimalDefinition();
                cmisPropertyDecimalDefinitionType cpd = (cmisPropertyDecimalDefinitionType)propDef;

                if (cpd.defaultValue != null && cpd.defaultValue.value != null)
                {
                    pd.DefaultValue = new List<decimal>();
                    foreach (decimal value in cpd.defaultValue.value) { pd.DefaultValue.Add(value); }
                }
                if (cpd.choice != null)
                {
                    pd.Choices = new List<IChoice<decimal>>();
                    foreach (cmisChoiceDecimal c in cpd.choice) { pd.Choices.Add(ConvertChoice(c)); }
                }

                if (cpd.maxValueSpecified)
                {
                    pd.MaxValue = cpd.maxValue;
                }
                if (cpd.minValueSpecified)
                {
                    pd.MinValue = cpd.minValue;
                }
                if (cpd.precisionSpecified)
                {
                    pd.Precision = (DecimalPrecision)CmisValue.SerializerToCmisEnum(cpd.precision);
                }

                ProcessAnyElements(cpd.Any, (element) =>
                {
                    if (element.LocalName.Equals("defaultValue"))
                    {
                        cmisChoiceDecimal prop = DeserializeElement<cmisChoiceDecimal>(element);
                        if (prop != null && prop.value != null)
                        {
                            pd.DefaultValue = new List<decimal>();
                            foreach (decimal value in prop.value) { pd.DefaultValue.Add(value); }
                        }
                    }
                    else if (element.LocalName.Equals("choice"))
                    {
                        cmisChoiceDecimal choice = DeserializeElement<cmisChoiceDecimal>(element);
                        if (choice != null)
                        {
                            if (pd.Choices == null) { pd.Choices = new List<IChoice<decimal>>(); }
                            pd.Choices.Add(ConvertChoice(choice));
                        }
                    }
                    else if (element.LocalName.Equals("maxValue"))
                    {
                        pd.MaxValue = DeserializeDecimal(element);
                    }
                    else if (element.LocalName.Equals("minValue"))
                    {
                        pd.MinValue = DeserializeDecimal(element);
                    }
                    else if (element.LocalName.Equals("precision"))
                    {
                        pd.Precision = DeserializeEnum<DecimalPrecision>(element);
                    }
                });

                result = pd;
            }
            else if (propDef is cmisPropertyHtmlDefinitionType)
            {
                PropertyHtmlDefinition pd = new PropertyHtmlDefinition();
                cmisPropertyHtmlDefinitionType cpd = (cmisPropertyHtmlDefinitionType)propDef;

                if (cpd.defaultValue != null && cpd.defaultValue.value != null)
                {
                    pd.DefaultValue = new List<string>();
                    foreach (string value in cpd.defaultValue.value) { pd.DefaultValue.Add(value); }
                }
                if (cpd.choice != null)
                {
                    pd.Choices = new List<IChoice<string>>();
                    foreach (cmisChoiceHtml c in cpd.choice) { pd.Choices.Add(ConvertChoice(c)); }
                }

                ProcessAnyElements(cpd.Any, (element) =>
                {
                    if (element.LocalName.Equals("defaultValue"))
                    {
                        cmisPropertyHtml prop = DeserializeElement<cmisPropertyHtml>(element);
                        if (prop != null && prop.value != null)
                        {
                            pd.DefaultValue = new List<string>();
                            foreach (string value in prop.value) { pd.DefaultValue.Add(value); }
                        }
                    }
                    else if (element.LocalName.Equals("choice"))
                    {
                        cmisChoiceHtml choice = DeserializeElement<cmisChoiceHtml>(element);
                        if (choice != null)
                        {
                            if (pd.Choices == null) { pd.Choices = new List<IChoice<string>>(); }
                            pd.Choices.Add(ConvertChoice(choice));
                        }
                    }
                });

                result = pd;
            }
            else if (propDef is cmisPropertyIdDefinitionType)
            {
                PropertyIdDefinition pd = new PropertyIdDefinition();
                cmisPropertyIdDefinitionType cpd = (cmisPropertyIdDefinitionType)propDef;

                if (cpd.defaultValue != null && cpd.defaultValue.value != null)
                {
                    pd.DefaultValue = new List<string>();
                    foreach (string value in cpd.defaultValue.value) { pd.DefaultValue.Add(value); }
                }
                if (cpd.choice != null)
                {
                    pd.Choices = new List<IChoice<string>>();
                    foreach (cmisChoiceId c in cpd.choice) { pd.Choices.Add(ConvertChoice(c)); }
                }

                ProcessAnyElements(cpd.Any, (element) =>
                {
                    if (element.LocalName.Equals("defaultValue"))
                    {
                        cmisPropertyId prop = DeserializeElement<cmisPropertyId>(element);
                        if (prop != null && prop.value != null)
                        {
                            pd.DefaultValue = new List<string>();
                            foreach (string value in prop.value) { pd.DefaultValue.Add(value); }
                        }
                    }
                    else if (element.LocalName.Equals("choice"))
                    {
                        cmisChoiceId choice = DeserializeElement<cmisChoiceId>(element);
                        if (choice != null)
                        {
                            if (pd.Choices == null) { pd.Choices = new List<IChoice<string>>(); }
                            pd.Choices.Add(ConvertChoice(choice));
                        }
                    }
                });

                result = pd;
            }
            else if (propDef is cmisPropertyIntegerDefinitionType)
            {
                PropertyIntegerDefinition pd = new PropertyIntegerDefinition();
                cmisPropertyIntegerDefinitionType cpd = (cmisPropertyIntegerDefinitionType)propDef;

                if (cpd.defaultValue != null && cpd.defaultValue.value != null)
                {
                    pd.DefaultValue = new List<long>();
                    foreach (string value in cpd.defaultValue.value) { pd.DefaultValue.Add(Int64.Parse(value)); }
                }
                if (cpd.choice != null)
                {
                    pd.Choices = new List<IChoice<long>>();
                    foreach (cmisChoiceInteger c in cpd.choice) { pd.Choices.Add(ConvertChoice(c)); }
                }

                if (cpd.maxValue != null)
                {
                    pd.MaxValue = Int64.Parse(cpd.maxValue);
                }
                if (cpd.minValue != null)
                {
                    pd.MinValue = Int64.Parse(cpd.minValue);
                }

                ProcessAnyElements(cpd.Any, (element) =>
                {
                    if (element.LocalName.Equals("defaultValue"))
                    {
                        cmisPropertyInteger prop = DeserializeElement<cmisPropertyInteger>(element);
                        if (prop != null && prop.value != null)
                        {
                            pd.DefaultValue = new List<long>();
                            foreach (string value in prop.value) { pd.DefaultValue.Add(Int64.Parse(value)); }
                        }
                    }
                    else if (element.LocalName.Equals("choice"))
                    {
                        cmisChoiceInteger choice = DeserializeElement<cmisChoiceInteger>(element);
                        if (choice != null)
                        {
                            if (pd.Choices == null) { pd.Choices = new List<IChoice<long>>(); }
                            pd.Choices.Add(ConvertChoice(choice));
                        }
                    }
                    else if (element.LocalName.Equals("maxValue"))
                    {
                        pd.MaxValue = DeserializeInteger(element);
                    }
                    else if (element.LocalName.Equals("minValue"))
                    {
                        pd.MinValue = DeserializeInteger(element);
                    }
                });

                result = pd;
            }
            else if (propDef is cmisPropertyStringDefinitionType)
            {
                PropertyStringDefinition pd = new PropertyStringDefinition();
                cmisPropertyStringDefinitionType cpd = (cmisPropertyStringDefinitionType)propDef;

                if (cpd.defaultValue != null && cpd.defaultValue.value != null)
                {
                    pd.DefaultValue = new List<string>();
                    foreach (string value in cpd.defaultValue.value) { pd.DefaultValue.Add(value); }
                }
                if (cpd.choice != null)
                {
                    pd.Choices = new List<IChoice<string>>();
                    foreach (cmisChoiceString c in cpd.choice) { pd.Choices.Add(ConvertChoice(c)); }
                }

                if (cpd.maxLength != null)
                {
                    pd.MaxLength = Int64.Parse(cpd.maxLength);
                }

                ProcessAnyElements(cpd.Any, (element) =>
                {
                    if (element.LocalName.Equals("defaultValue"))
                    {
                        cmisPropertyString prop = DeserializeElement<cmisPropertyString>(element);
                        if (prop != null && prop.value != null)
                        {
                            pd.DefaultValue = new List<string>();
                            foreach (string value in prop.value) { pd.DefaultValue.Add(value); }
                        }
                    }
                    else if (element.LocalName.Equals("choice"))
                    {
                        cmisChoiceString choice = DeserializeElement<cmisChoiceString>(element);
                        if (choice != null)
                        {
                            if (pd.Choices == null)
                            {
                                pd.Choices = new List<IChoice<string>>();
                            }
                            pd.Choices.Add(ConvertChoice(choice));
                        }
                    }
                    else if (element.LocalName.Equals("maxLength"))
                    {
                        pd.MaxLength = DeserializeInteger(element);
                    }
                });

                result = pd;
            }
            else if (propDef is cmisPropertyUriDefinitionType)
            {
                PropertyUriDefinition pd = new PropertyUriDefinition();
                cmisPropertyUriDefinitionType cpd = (cmisPropertyUriDefinitionType)propDef;

                if (cpd.defaultValue != null && cpd.defaultValue.value != null)
                {
                    pd.DefaultValue = new List<string>();
                    foreach (string value in cpd.defaultValue.value) { pd.DefaultValue.Add(value); }
                }
                if (cpd.choice != null)
                {
                    pd.Choices = new List<IChoice<string>>();
                    foreach (cmisChoiceUri c in cpd.choice) { pd.Choices.Add(ConvertChoice(c)); }
                }

                ProcessAnyElements(cpd.Any, (element) =>
                {
                    if (element.LocalName.Equals("defaultValue"))
                    {
                        cmisPropertyUri prop = DeserializeElement<cmisPropertyUri>(element);
                        if (prop != null && prop.value != null)
                        {
                            pd.DefaultValue = new List<string>();
                            foreach (string value in prop.value) { pd.DefaultValue.Add(value); }
                        }
                    }
                    else if (element.LocalName.Equals("choice"))
                    {
                        cmisChoiceUri choice = DeserializeElement<cmisChoiceUri>(element);
                        if (choice != null)
                        {
                            if (pd.Choices == null)
                            {
                                pd.Choices = new List<IChoice<string>>();
                            }
                            pd.Choices.Add(ConvertChoice(choice));
                        }
                    }
                });

                result = pd;
            }

            result.Id = propDef.id;
            result.LocalName = propDef.localName;
            result.LocalNamespace = propDef.localNamespace;
            result.DisplayName = propDef.displayName;
            result.QueryName = propDef.queryName;
            result.Description = propDef.description;
            result.PropertyType = (PropertyType)CmisValue.SerializerToCmisEnum(propDef.propertyType);
            result.Cardinality = (Cardinality)CmisValue.SerializerToCmisEnum(propDef.cardinality);
            result.Updatability = (Updatability)CmisValue.SerializerToCmisEnum(propDef.updatability);
            result.IsInherited = (propDef.inheritedSpecified ? (bool?)propDef.inherited : null);
            result.IsRequired = propDef.required;
            result.IsQueryable = propDef.queryable;
            result.IsOrderable = propDef.orderable;
            result.IsOpenChoice = (propDef.openChoiceSpecified ? (bool?)propDef.openChoice : null);

            ConvertExtension(propDef, result);

            return result;
        }

        private static IChoice<bool> ConvertChoice(cmisChoiceBoolean choice)
        {
            if (choice == null) { return null; }

            Choice<bool> result = new Choice<bool>();
            result.DisplayName = choice.displayName;
            if (choice.value != null)
            {
                result.Value = new List<bool>();
                foreach (bool v in choice.value) { result.Value.Add(v); }
            }
            if (choice.choice != null)
            {
                result.Choices = new List<IChoice<bool>>();
                foreach (cmisChoiceBoolean sc in choice.choice) { result.Choices.Add(ConvertChoice(sc)); }
            }

            return result;
        }

        private static IChoice<DateTime> ConvertChoice(cmisChoiceDateTime choice)
        {
            if (choice == null) { return null; }

            Choice<DateTime> result = new Choice<DateTime>();
            result.DisplayName = choice.displayName;
            if (choice.value != null)
            {
                result.Value = new List<DateTime>();
                foreach (DateTime v in choice.value) { result.Value.Add(v); }
            }
            if (choice.choice != null)
            {
                result.Choices = new List<IChoice<DateTime>>();
                foreach (cmisChoiceDateTime sc in choice.choice) { result.Choices.Add(ConvertChoice(sc)); }
            }

            return result;
        }

        private static IChoice<decimal> ConvertChoice(cmisChoiceDecimal choice)
        {
            if (choice == null) { return null; }

            Choice<decimal> result = new Choice<decimal>();
            result.DisplayName = choice.displayName;
            if (choice.value != null)
            {
                result.Value = new List<decimal>();
                foreach (decimal v in choice.value) { result.Value.Add(v); }
            }
            if (choice.choice != null)
            {
                result.Choices = new List<IChoice<decimal>>();
                foreach (cmisChoiceDecimal sc in choice.choice) { result.Choices.Add(ConvertChoice(sc)); }
            }

            return result;
        }

        private static IChoice<string> ConvertChoice(cmisChoiceHtml choice)
        {
            if (choice == null) { return null; }

            Choice<string> result = new Choice<string>();
            result.DisplayName = choice.displayName;
            if (choice.value != null)
            {
                result.Value = new List<string>();
                foreach (string v in choice.value) { result.Value.Add(v); }
            }
            if (choice.choice != null)
            {
                result.Choices = new List<IChoice<string>>();
                foreach (cmisChoiceHtml sc in choice.choice) { result.Choices.Add(ConvertChoice(sc)); }
            }

            return result;
        }

        private static IChoice<string> ConvertChoice(cmisChoiceId choice)
        {
            if (choice == null) { return null; }

            Choice<string> result = new Choice<string>();
            result.DisplayName = choice.displayName;
            if (choice.value != null)
            {
                result.Value = new List<string>();
                foreach (string v in choice.value) { result.Value.Add(v); }
            }
            if (choice.choice != null)
            {
                result.Choices = new List<IChoice<string>>();
                foreach (cmisChoiceId sc in choice.choice) { result.Choices.Add(ConvertChoice(sc)); }
            }

            return result;
        }

        private static IChoice<long> ConvertChoice(cmisChoiceInteger choice)
        {
            if (choice == null) { return null; }

            Choice<long> result = new Choice<long>();
            result.DisplayName = choice.displayName;
            if (choice.value != null)
            {
                result.Value = new List<long>();
                foreach (string v in choice.value) { result.Value.Add(Int64.Parse(v)); }
            }
            if (choice.choice != null)
            {
                result.Choices = new List<IChoice<long>>();
                foreach (cmisChoiceInteger sc in choice.choice) { result.Choices.Add(ConvertChoice(sc)); }
            }

            return result;
        }

        private static IChoice<string> ConvertChoice(cmisChoiceString choice)
        {
            if (choice == null) { return null; }

            Choice<string> result = new Choice<string>();
            result.DisplayName = choice.displayName;
            if (choice.value != null)
            {
                result.Value = new List<string>();
                foreach (string v in choice.value) { result.Value.Add(v); }
            }
            if (choice.choice != null)
            {
                result.Choices = new List<IChoice<string>>();
                foreach (cmisChoiceString sc in choice.choice) { result.Choices.Add(ConvertChoice(sc)); }
            }

            return result;
        }

        private static IChoice<string> ConvertChoice(cmisChoiceUri choice)
        {
            if (choice == null) { return null; }

            Choice<string> result = new Choice<string>();
            result.DisplayName = choice.displayName;
            if (choice.value != null)
            {
                result.Value = new List<string>();
                foreach (string v in choice.value) { result.Value.Add(v); }
            }
            if (choice.choice != null)
            {
                result.Choices = new List<IChoice<string>>();
                foreach (cmisChoiceUri sc in choice.choice) { result.Choices.Add(ConvertChoice(sc)); }
            }

            return result;
        }


        /// <summary>
        /// Converts a type defintion list.
        /// </summary> 
        public static ITypeDefinitionList Convert(cmisTypeDefinitionListType typeDefList)
        {
            if (typeDefList == null) { return null; }

            TypeDefinitionList result = new TypeDefinitionList();

            if (typeDefList.types != null)
            {
                result.List = new List<ITypeDefinition>();
                foreach (cmisTypeDefinitionType type in typeDefList.types)
                {
                    result.List.Add(Convert(type));
                }
            }

            result.HasMoreItems = typeDefList.hasMoreItems;
            result.NumItems = typeDefList.numItems == null ? null : (long?)Int64.Parse(typeDefList.numItems);

            ConvertExtension(typeDefList, result);

            return result;
        }

        /// <summary>
        /// Converts a type defintion container.
        /// </summary> 
        public static ITypeDefinitionContainer Convert(cmisTypeContainer typeDefCont)
        {
            if (typeDefCont == null) { return null; }

            TypeDefinitionContainer result = new TypeDefinitionContainer();
            result.TypeDefinition = Convert(typeDefCont.type);
            if (typeDefCont.children != null)
            {
                result.Children = new List<ITypeDefinitionContainer>();
                foreach (cmisTypeContainer container in typeDefCont.children)
                {
                    result.Children.Add(Convert(container));
                }
            }

            ConvertExtension(typeDefCont, result);

            return result;
        }

        public static IObjectData Convert(cmisObjectType cmisObject)
        {
            if (cmisObject == null) { return null; }

            ObjectData result = new ObjectData();
            result.Properties = Convert(cmisObject.properties);
            result.AllowableActions = Convert(cmisObject.allowableActions);
            if (cmisObject.relationship != null)
            {
                result.Relationships = new List<IObjectData>();
                foreach (cmisObjectType co in cmisObject.relationship)
                {
                    result.Relationships.Add(Convert(co));
                }
            }
            result.ChangeEventInfo = Convert(cmisObject.changeEventInfo);
            result.IsExactAcl = cmisObject.exactACLSpecified ? (bool?)cmisObject.exactACL : null;
            result.Acl = Convert(cmisObject.acl, result.IsExactAcl);
            result.PolicyIds = Convert(cmisObject.policyIds);
            if (cmisObject.rendition != null)
            {
                result.Renditions = new List<IRenditionData>();
                foreach (cmisRenditionType rendition in cmisObject.rendition)
                {
                    result.Renditions.Add(Convert(rendition));
                }
            }

            ConvertExtension(cmisObject, result);

            return result;
        }

        public static IProperties Convert(cmisPropertiesType properties)
        {
            if (properties == null) { return null; }

            Properties result = new Properties();
            if (properties.Items != null)
            {
                foreach (cmisProperty property in properties.Items)
                {
                    result.AddProperty(Convert(property));
                }
            }

            ConvertExtension(properties, result);

            return result;
        }
        public static IPropertyData Convert(cmisProperty property)
        {
            if (property == null) { return null; }

            PropertyData result = null;
            if (property is cmisPropertyString)
            {
                result = new PropertyData(PropertyType.String);
                if (((cmisPropertyString)property).value != null)
                {
                    foreach (string value in ((cmisPropertyString)property).value)
                    {
                        result.AddValue(value);
                    }
                }
            }
            else if (property is cmisPropertyId)
            {
                result = new PropertyData(PropertyType.Id);
                if (((cmisPropertyId)property).value != null)
                {
                    foreach (string value in ((cmisPropertyId)property).value)
                    {
                        result.AddValue(value);
                    }
                }
            }
            else if (property is cmisPropertyInteger)
            {
                result = new PropertyData(PropertyType.Integer);
                if (((cmisPropertyInteger)property).value != null)
                {
                    foreach (string value in ((cmisPropertyInteger)property).value)
                    {
                        result.AddValue(Int64.Parse(value));
                    }
                }
            }
            else if (property is cmisPropertyBoolean)
            {
                result = new PropertyData(PropertyType.Boolean);
                if (((cmisPropertyBoolean)property).value != null)
                {
                    foreach (bool value in ((cmisPropertyBoolean)property).value)
                    {
                        result.AddValue(value);
                    }
                }
            }
            else if (property is cmisPropertyDateTime)
            {
                result = new PropertyData(PropertyType.DateTime);
                if (((cmisPropertyDateTime)property).value != null)
                {
                    foreach (DateTime value in ((cmisPropertyDateTime)property).value)
                    {
                        result.AddValue(value);
                    }
                }
            }
            else if (property is cmisPropertyDecimal)
            {
                result = new PropertyData(PropertyType.Decimal);
                if (((cmisPropertyDecimal)property).value != null)
                {
                    foreach (decimal value in ((cmisPropertyDecimal)property).value)
                    {
                        result.AddValue(value);
                    }
                }
            }
            else if (property is cmisPropertyHtml)
            {
                result = new PropertyData(PropertyType.Html);
                if (((cmisPropertyHtml)property).value != null)
                {
                    foreach (string value in ((cmisPropertyHtml)property).value)
                    {
                        result.AddValue(value);
                    }
                }
            }
            else if (property is cmisPropertyUri)
            {
                result = new PropertyData(PropertyType.Uri);
                if (((cmisPropertyUri)property).value != null)
                {
                    foreach (string value in ((cmisPropertyUri)property).value)
                    {
                        result.AddValue(value);
                    }
                }
            }

            result.Id = property.propertyDefinitionId;
            result.LocalName = property.localName;
            result.DisplayName = property.displayName;
            result.QueryName = property.queryName;

            ConvertExtension(property, result);

            return result;
        }


        public static cmisPropertiesType Convert(IProperties properties)
        {
            if (properties == null) { return null; }

            cmisPropertiesType result = new cmisPropertiesType();
            if (properties.PropertyList != null)
            {
                result.Items = new cmisProperty[properties.PropertyList.Count];
                for (int i = 0; i < properties.PropertyList.Count; i++)
                {
                    result.Items[i] = Convert(properties.PropertyList[i]);
                }
            }

            ConvertExtension(properties, result);

            return result;
        }

        public static cmisProperty Convert(IPropertyData property)
        {
            if (property == null) { return null; }

            cmisProperty result = null;

            switch (property.PropertyType)
            {
                case PropertyType.String:
                    result = new cmisPropertyString();
                    if (property.Values != null)
                    {
                        ((cmisPropertyString)result).value = new string[property.Values.Count];
                        for (int i = 0; i < property.Values.Count; i++)
                        {
                            ((cmisPropertyString)result).value[i] = property.Values[i] as string;
                        }
                    }
                    break;
                case PropertyType.Id:
                    result = new cmisPropertyId();
                    if (property.Values != null)
                    {
                        ((cmisPropertyId)result).value = new string[property.Values.Count];
                        for (int i = 0; i < property.Values.Count; i++)
                        {
                            ((cmisPropertyId)result).value[i] = property.Values[i] as string;
                        }
                    }
                    break;
                case PropertyType.Integer:
                    result = new cmisPropertyInteger();
                    if (property.Values != null)
                    {
                        ((cmisPropertyInteger)result).value = new string[property.Values.Count];
                        for (int i = 0; i < property.Values.Count; i++)
                        {
                            long? value = property.Values[i] as long?;
                            if (value.HasValue)
                            {
                                ((cmisPropertyInteger)result).value[i] = value.ToString();
                            }
                        }
                    }
                    break;
                case PropertyType.Boolean:
                    result = new cmisPropertyBoolean();
                    if (property.Values != null)
                    {
                        ((cmisPropertyBoolean)result).value = new bool[property.Values.Count];
                        for (int i = 0; i < property.Values.Count; i++)
                        {
                            bool? value = property.Values[i] as bool?;
                            if (value.HasValue)
                            {
                                ((cmisPropertyBoolean)result).value[i] = value.Value;
                            }
                        }
                    }
                    break;
                case PropertyType.DateTime:
                    result = new cmisPropertyDateTime();
                    if (property.Values != null)
                    {
                        ((cmisPropertyDateTime)result).value = new DateTime[property.Values.Count];
                        for (int i = 0; i < property.Values.Count; i++)
                        {
                            DateTime? value = property.Values[i] as DateTime?;
                            if (value.HasValue)
                            {
                                ((cmisPropertyDateTime)result).value[i] = value.Value;
                            }
                        }
                    }
                    break;
                case PropertyType.Decimal:
                    result = new cmisPropertyDecimal();
                    if (property.Values != null)
                    {
                        ((cmisPropertyDecimal)result).value = new decimal[property.Values.Count];
                        for (int i = 0; i < property.Values.Count; i++)
                        {
                            decimal? value = property.Values[i] as decimal?;
                            if (value.HasValue)
                            {
                                ((cmisPropertyDecimal)result).value[i] = value.Value;
                            }
                        }
                    }
                    break;
                case PropertyType.Html:
                    result = new cmisPropertyHtml();
                    if (property.Values != null)
                    {
                        ((cmisPropertyHtml)result).value = new string[property.Values.Count];
                        for (int i = 0; i < property.Values.Count; i++)
                        {
                            ((cmisPropertyHtml)result).value[i] = property.Values[i] as string;
                        }
                    }
                    break;
                case PropertyType.Uri:
                    result = new cmisPropertyUri();
                    if (property.Values != null)
                    {
                        ((cmisPropertyUri)result).value = new string[property.Values.Count];
                        for (int i = 0; i < property.Values.Count; i++)
                        {
                            ((cmisPropertyUri)result).value[i] = property.Values[i] as string;
                        }
                    }
                    break;
            }

            result.propertyDefinitionId = property.Id;
            result.localName = property.LocalName;
            result.displayName = property.DisplayName;
            result.queryName = property.QueryName;

            ConvertExtension(property, result);

            return result;
        }

        public static IAllowableActions Convert(cmisAllowableActionsType allowableActions)
        {
            if (allowableActions == null) { return null; }

            AllowableActions result = new AllowableActions();
            result.Actions = new HashSet<string>();

            if (allowableActions.canDeleteObject && allowableActions.canDeleteObjectSpecified)
            { result.Actions.Add(Actions.CanDeleteObject); }

            if (allowableActions.canUpdateProperties && allowableActions.canUpdatePropertiesSpecified)
            { result.Actions.Add(Actions.CanUpdateProperties); }

            if (allowableActions.canGetFolderTree && allowableActions.canGetFolderTreeSpecified)
            { result.Actions.Add(Actions.CanGetFolderTree); }

            if (allowableActions.canGetProperties && allowableActions.canGetPropertiesSpecified)
            { result.Actions.Add(Actions.CanGetProperties); }

            if (allowableActions.canGetObjectRelationships && allowableActions.canGetObjectRelationshipsSpecified)
            { result.Actions.Add(Actions.CanGetObjectRelationships); }

            if (allowableActions.canGetObjectParents && allowableActions.canGetObjectParentsSpecified)
            { result.Actions.Add(Actions.CanGetObjectParents); }

            if (allowableActions.canGetFolderParent && allowableActions.canGetFolderParentSpecified)
            { result.Actions.Add(Actions.CanGetFolderParent); }

            if (allowableActions.canGetDescendants && allowableActions.canGetDescendantsSpecified)
            { result.Actions.Add(Actions.CanGetDescendants); }

            if (allowableActions.canMoveObject && allowableActions.canMoveObjectSpecified)
            { result.Actions.Add(Actions.CanMoveObject); }

            if (allowableActions.canDeleteContentStream && allowableActions.canDeleteContentStreamSpecified)
            { result.Actions.Add(Actions.CanDeleteContentStream); }

            if (allowableActions.canCheckOut && allowableActions.canCheckOutSpecified)
            { result.Actions.Add(Actions.CanCheckOut); }

            if (allowableActions.canCancelCheckOut && allowableActions.canCancelCheckOutSpecified)
            { result.Actions.Add(Actions.CanCancelCheckOut); }

            if (allowableActions.canCheckIn && allowableActions.canCheckInSpecified)
            { result.Actions.Add(Actions.CanCheckIn); }

            if (allowableActions.canSetContentStream && allowableActions.canSetContentStreamSpecified)
            { result.Actions.Add(Actions.CanSetContentStream); }

            if (allowableActions.canGetAllVersions && allowableActions.canGetAllVersionsSpecified)
            { result.Actions.Add(Actions.CanGetAllVersions); }

            if (allowableActions.canAddObjectToFolder && allowableActions.canAddObjectToFolderSpecified)
            { result.Actions.Add(Actions.CanAddObjectToFolder); }

            if (allowableActions.canRemoveObjectFromFolder && allowableActions.canRemoveObjectFromFolderSpecified)
            { result.Actions.Add(Actions.CanRemoveObjectFromFolder); }

            if (allowableActions.canGetContentStream && allowableActions.canGetContentStreamSpecified)
            { result.Actions.Add(Actions.CanGetContentStream); }

            if (allowableActions.canApplyPolicy && allowableActions.canApplyPolicySpecified)
            { result.Actions.Add(Actions.CanApplyPolicy); }

            if (allowableActions.canGetAppliedPolicies && allowableActions.canGetAppliedPoliciesSpecified)
            { result.Actions.Add(Actions.CanGetAppliedPolicies); }

            if (allowableActions.canRemovePolicy && allowableActions.canRemovePolicySpecified)
            { result.Actions.Add(Actions.CanRemovePolicy); }

            if (allowableActions.canGetChildren && allowableActions.canGetChildrenSpecified)
            { result.Actions.Add(Actions.CanGetChildren); }

            if (allowableActions.canCreateDocument && allowableActions.canCreateDocumentSpecified)
            { result.Actions.Add(Actions.CanCreateDocument); }

            if (allowableActions.canCreateFolder && allowableActions.canCreateFolderSpecified)
            { result.Actions.Add(Actions.CanCreateFolder); }

            if (allowableActions.canCreateRelationship && allowableActions.canCreateRelationshipSpecified)
            { result.Actions.Add(Actions.CanCreateRelationship); }

            if (allowableActions.canDeleteTree && allowableActions.canDeleteTreeSpecified)
            { result.Actions.Add(Actions.CanDeleteTree); }

            if (allowableActions.canGetRenditions && allowableActions.canGetRenditionsSpecified)
            { result.Actions.Add(Actions.CanGetRenditions); }

            if (allowableActions.canGetACL && allowableActions.canGetACLSpecified)
            { result.Actions.Add(Actions.CanGetAcl); }

            if (allowableActions.canApplyACL && allowableActions.canApplyACLSpecified)
            { result.Actions.Add(Actions.CanApplyAcl); }

            ConvertExtension(allowableActions, result);

            return result;
        }

        public static IAcl Convert(cmisAccessControlListType acl, bool? isExact)
        {
            if (acl == null) { return null; }

            Acl result = new Acl();
            if (acl.permission != null)
            {
                result.Aces = new List<IAce>();
                foreach (cmisAccessControlEntryType ace in acl.permission)
                {
                    result.Aces.Add(Convert(ace));
                }
            }
            result.IsExact = isExact;

            ConvertExtension(acl, result);

            return result;
        }


        public static IAcl Convert(cmisACLType acl)
        {
            if (acl == null) { return null; }

            Acl result = new Acl();
            if (acl.ACL != null && acl.ACL.permission != null)
            {
                result.Aces = new List<IAce>();
                foreach (cmisAccessControlEntryType ace in acl.ACL.permission)
                {
                    result.Aces.Add(Convert(ace));
                }
            }
            result.IsExact = (acl.exactSpecified ? (bool?)acl.exactSpecified : null);

            ConvertExtension(acl, result);

            return result;
        }


        public static IAce Convert(cmisAccessControlEntryType ace)
        {
            if (ace == null) { return null; }

            Ace result = new Ace();
            if (ace.principal != null)
            {
                Principal principal = new Principal();
                principal.Id = ace.principal.principalId;
                result.Principal = principal;

                ConvertExtension(ace.principal, principal);
            }
            if (ace.permission != null)
            {
                result.Permissions = new List<string>();
                foreach (string permission in ace.permission)
                {
                    result.Permissions.Add(permission);
                }
            }
            result.IsDirect = ace.direct;

            ConvertExtension(ace, result);

            return result;
        }

        public static cmisAccessControlListType Convert(IAcl acl)
        {
            if (acl == null) { return null; }

            cmisAccessControlListType result = new cmisAccessControlListType();
            if (acl.Aces != null)
            {
                result.permission = new cmisAccessControlEntryType[acl.Aces.Count];
                for (int i = 0; i < acl.Aces.Count; i++)
                {
                    result.permission[i] = Convert(acl.Aces[i]);
                }
            }

            ConvertExtension(acl, result);

            return result;
        }

        public static cmisAccessControlEntryType Convert(IAce ace)
        {
            if (ace == null) { return null; }

            cmisAccessControlEntryType result = new cmisAccessControlEntryType();
            if (ace.Principal != null)
            {
                result.principal = new cmisAccessControlPrincipalType();
                result.principal.principalId = ace.Principal.Id;

                ConvertExtension(ace.Principal, result.principal);
            }
            if (ace.Permissions != null)
            {
                result.permission = new string[ace.Permissions.Count];
                for (int i = 0; i < ace.Permissions.Count; i++)
                {
                    result.permission[i] = ace.Permissions[i];
                }
            }
            result.direct = ace.IsDirect;

            ConvertExtension(ace, result);

            return result;
        }


        public static IPolicyIdList Convert(cmisListOfIdsType policyIdList)
        {
            if (policyIdList == null) { return null; }

            PolicyIdList result = new PolicyIdList();
            if (policyIdList.id != null)
            {
                result.PolicyIds = new List<string>();
                foreach (string id in policyIdList.id)
                {
                    result.PolicyIds.Add(id);
                }
            }

            ConvertExtension(policyIdList, result);

            return result;
        }

        public static cmisListOfIdsType ConvertPolicies(IList<string> policyIds)
        {
            if (policyIds == null) { return null; }

            cmisListOfIdsType result = new cmisListOfIdsType();
            result.id = new string[policyIds.Count];
            for (int i = 0; i < policyIds.Count; i++)
            {
                result.id[i] = policyIds[i];
            }

            return result;
        }


        public static IRenditionData Convert(cmisRenditionType rendition)
        {
            if (rendition == null) { return null; }

            RenditionData result = new RenditionData();
            result.StreamId = rendition.streamId;
            result.MimeType = rendition.mimetype;
            result.Length = rendition.length == null ? null : (long?)Int64.Parse(rendition.length);
            result.Kind = rendition.kind;
            result.Title = rendition.title;
            result.Height = rendition.height == null ? null : (long?)Int64.Parse(rendition.height);
            result.Width = rendition.width == null ? null : (long?)Int64.Parse(rendition.width);
            result.RenditionDocumentId = rendition.renditionDocumentId;

            ConvertExtension(rendition, result);

            return result;
        }

        public static IChangeEventInfo Convert(cmisChangeEventType changeEvent)
        {
            if (changeEvent == null) { return null; }

            ChangeEventInfo result = new ChangeEventInfo();
            result.ChangeType = (ChangeType)CmisValue.SerializerToCmisEnum(changeEvent.changeType);
            result.ChangeTime = changeEvent.changeTime;

            ConvertExtension(changeEvent, result);

            return result;
        }

        public static IObjectInFolderList Convert(cmisObjectInFolderListType list)
        {
            if (list == null) { return null; }

            ObjectInFolderList result = new ObjectInFolderList();
            if (list.objects != null)
            {
                result.Objects = new List<IObjectInFolderData>();
                foreach (cmisObjectInFolderType fo in list.objects)
                {
                    result.Objects.Add(Convert(fo));
                }
            }
            result.HasMoreItems = list.hasMoreItems;
            result.NumItems = list.numItems == null ? null : (long?)Int64.Parse(list.numItems);

            ConvertExtension(list, result);

            return result;
        }


        public static IObjectInFolderData Convert(cmisObjectInFolderType objectInFolder)
        {
            if (objectInFolder == null) { return null; }

            ObjectInFolderData result = new ObjectInFolderData();
            result.Object = Convert(objectInFolder.@object);
            result.PathSegment = objectInFolder.pathSegment;

            ConvertExtension(objectInFolder, result);

            return result;
        }

        public static IObjectInFolderContainer Convert(cmisObjectInFolderContainerType container)
        {
            if (container == null) { return null; }

            ObjectInFolderContainer result = new ObjectInFolderContainer();
            result.Object = Convert(container.objectInFolder);
            if (container.children != null)
            {
                result.Children = new List<IObjectInFolderContainer>();
                foreach (cmisObjectInFolderContainerType child in container.children)
                {
                    result.Children.Add(Convert(child));
                }
            }

            ConvertExtension(container, result);

            return result;
        }

        public static IObjectParentData Convert(cmisObjectParentsType parent)
        {
            if (parent == null) { return null; }

            ObjectParentData result = new ObjectParentData();
            result.Object = Convert(parent.@object);
            result.RelativePathSegment = parent.relativePathSegment;

            ConvertExtension(parent, result);

            return result;
        }

        public static IObjectList Convert(cmisObjectListType list)
        {
            if (list == null) { return null; }

            ObjectList result = new ObjectList();
            if (list.objects != null)
            {
                result.Objects = new List<IObjectData>();
                foreach (cmisObjectType obj in list.objects)
                {
                    result.Objects.Add(Convert(obj));
                }
            }

            result.HasMoreItems = list.hasMoreItems;
            result.NumItems = list.numItems == null ? null : (long?)Int64.Parse(list.numItems);

            ConvertExtension(list, result);

            return result;
        }

        public static IContentStream Convert(cmisContentStreamType contentStream)
        {
            if (contentStream == null) { return null; }

            ContentStream result = new ContentStream();
            if (contentStream.length != null)
            {
                result.Length = Int64.Parse(contentStream.length);
            }
            result.MimeType = contentStream.mimeType;
            result.FileName = contentStream.filename;
            // Todo: enable real streaming
            result.Stream = new MemoryStream(contentStream.stream);

            ConvertExtension(contentStream, result);

            return result;
        }

        public static cmisContentStreamType Convert(IContentStream contentStream)
        {
            if (contentStream == null) { return null; }

            cmisContentStreamType result = new cmisContentStreamType();
            result.length = contentStream.Length.ToString();
            result.mimeType = contentStream.MimeType;
            result.filename = contentStream.FileName;
            if (contentStream.Stream != null)
            {
                if (contentStream.Stream is MemoryStream)
                {
                    result.stream = ((MemoryStream)contentStream.Stream).ToArray();
                }
                else
                {
                    MemoryStream memStream = new MemoryStream();
                    byte[] buffer = new byte[4096];
                    int bytes;
                    while ((bytes = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        memStream.Write(buffer, 0, bytes);
                    }
                    result.stream = memStream.ToArray();
                }
            }

            return result;
        }

        public static IFailedToDeleteData Convert(deleteTreeResponseFailedToDelete failedToDelete)
        {
            if (failedToDelete == null) { return null; }

            FailedToDeleteData result = new FailedToDeleteData();
            if (failedToDelete.objectIds != null)
            {
                result.Ids = new List<string>();
                foreach (string id in failedToDelete.objectIds)
                {
                    result.Ids.Add(id);
                }
            }

            ConvertExtension(failedToDelete, result);

            return result;
        }

        public static T[] ConvertList<T>(IList<T> list)
        {
            if (list == null) { return null; }

            T[] result = new T[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }

        /// <summary>
        /// Converts an extension.
        /// </summary> 
        public static void ConvertExtension(object source, IExtensionsData target)
        {
            if (source == null || target == null) { return; }

            Type type = source.GetType();
            PropertyInfo propInfo = type.GetProperty("Any");
            if (propInfo == null)
            {
                return;
            }

            XmlElement[] elements = (XmlElement[])propInfo.GetValue(source, null);
            if (elements == null)
            {
                return;
            }

            target.Extensions = new List<ICmisExtensionElement>();
            foreach (XmlElement xmlElement in elements)
            {
                ICmisExtensionElement element = CreateCmisExtensionElement(xmlElement);
                if (element != null)
                {
                    target.Extensions.Add(element);
                }
            }
        }

        private static ICmisExtensionElement CreateCmisExtensionElement(XmlNode xmlElement)
        {
            CmisExtensionElement result = new CmisExtensionElement();
            result.Name = xmlElement.Name;
            result.Namespace = xmlElement.NamespaceURI;

            if (xmlElement.Attributes != null && xmlElement.Attributes.Count > 0)
            {
                result.Attributes = new Dictionary<string, string>();
                foreach (XmlAttribute attr in xmlElement.Attributes)
                {
                    result.Attributes[attr.Name] = attr.Value;
                }
            }

            if (xmlElement.HasChildNodes)
            {
                result.Children = new List<ICmisExtensionElement>();
                foreach (XmlNode node in xmlElement.ChildNodes)
                {
                    if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA || node.NodeType == XmlNodeType.SignificantWhitespace)
                    {
                        result.Value = node.Value;
                    }
                    else if (node.NodeType == XmlNodeType.Element)
                    {
                        ICmisExtensionElement element = CreateCmisExtensionElement(node);
                        if (element != null)
                        {
                            result.Children.Add(element);
                        }
                    }
                }
            }
            else
            {
                result.Value = xmlElement.Value;
            }

            return result;
        }

        /// <summary>
        /// Converts an extension.
        /// </summary>
        public static cmisExtensionType ConvertExtension(IExtensionsData extension)
        {
            if (extension == null || extension.Extensions == null) { return null; }

            XmlDocument doc = new XmlDocument();

            List<XmlElement> elements = new List<XmlElement>();
            foreach (ICmisExtensionElement element in extension.Extensions)
            {
                if (element == null) { continue; }
                elements.Add(CreateXmlElement(doc, element));
            }

            cmisExtensionType result = new cmisExtensionType();
            result.Any = elements.ToArray();

            return result;
        }

        public static void ConvertExtension(IExtensionsData source, object target)
        {
            if (source == null || source.Extensions == null || target == null)
            {
                return;
            }

            Type type = target.GetType();
            PropertyInfo propInfo = type.GetProperty("Any");
            if (propInfo == null)
            {
                return;
            }

            XmlDocument doc = new XmlDocument();

            List<XmlElement> elements = new List<XmlElement>();
            foreach (ICmisExtensionElement element in source.Extensions)
            {
                if (element == null) { continue; }
                elements.Add(CreateXmlElement(doc, element));
            }

            propInfo.SetValue(target, elements.ToArray(), null);
        }

        private static XmlElement CreateXmlElement(XmlDocument doc, ICmisExtensionElement element)
        {
            if (element == null)
            {
                return null;
            }

            XmlElement result = doc.CreateElement(element.Name, element.Namespace);

            if (element.Attributes != null)
            {
                foreach (string key in element.Attributes.Keys)
                {
                    XmlAttribute attr = doc.CreateAttribute(key);
                    attr.Value = element.Attributes[key];
                    result.Attributes.Append(attr);
                }
            }

            if (element.Value != null)
            {
                result.InnerText = element.Value;
            }
            else if (element.Children != null)
            {
                List<XmlElement> children = new List<XmlElement>();
                foreach (ICmisExtensionElement child in element.Children)
                {
                    XmlElement xml = CreateXmlElement(doc, child);
                    if (xml != null)
                    {
                        result.AppendChild(xml);
                    }
                }
            }

            return result;
        }

        public static string GetXmlEnumAttributeValue(Enum xsdEnum)
        {
            FieldInfo fieldInfo = xsdEnum.GetType().GetField(xsdEnum.ToString());
            XmlEnumAttribute[] xmlAttr = fieldInfo.GetCustomAttributes(typeof(XmlEnumAttribute), false) as XmlEnumAttribute[];

            if (xmlAttr.Length == 0)
            {
                return null;
            }

            return xmlAttr[0].Name;
        }
    }
}
