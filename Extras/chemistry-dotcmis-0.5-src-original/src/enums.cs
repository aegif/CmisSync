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
using System.Xml.Serialization;
using DotCMIS.CMISWebServicesReference;

namespace DotCMIS.Enums
{
    public enum BaseTypeId
    {
        [CmisValue("cmis:document")]
        CmisDocument,

        [CmisValue("cmis:folder")]
        CmisFolder,

        [CmisValue("cmis:relationship")]
        CmisRelationship,

        [CmisValue("cmis:policy")]
        CmisPolicy
    }

    public enum CapabilityContentStreamUpdates
    {
        [CmisValue("anytime")]
        Anyime,

        [CmisValue("pwconly")]
        PWCOnly,

        [CmisValue("none")]
        None
    }

    public enum CapabilityChanges
    {
        [CmisValue("none")]
        None,

        [CmisValue("objectidsonly")]
        ObjectIdsOnly,

        [CmisValue("properties")]
        Properties,

        [CmisValue("all")]
        All
    }

    public enum CapabilityRenditions
    {
        [CmisValue("none")]
        None,

        [CmisValue("read")]
        Read
    }

    public enum CapabilityQuery
    {
        [CmisValue("none")]
        None,

        [CmisValue("metadataonly")]
        MetadataOnly,

        [CmisValue("fulltextonly")]
        FulltextOnly,

        [CmisValue("bothseparate")]
        BothSeparate,

        [CmisValue("bothcombined")]
        BothCombined
    }

    public enum CapabilityJoin
    {
        [CmisValue("none")]
        None,

        [CmisValue("inneronly")]
        InnerOnly,

        [CmisValue("innerandouter")]
        InnerAndOuter
    }

    public enum CapabilityAcl
    {
        [CmisValue("none")]
        None,

        [CmisValue("discover")]
        Discover,

        [CmisValue("manage")]
        Manage
    }

    public enum SupportedPermissions
    {
        [CmisValue("basic")]
        Basic,

        [CmisValue("repository")]
        Repository,

        [CmisValue("both")]
        Both
    }

    public enum AclPropagation
    {
        [CmisValue("repositorydetermined")]
        RepositoryDetermined,

        [CmisValue("objectonly")]
        ObjectOnly,

        [CmisValue("propagate")]
        Propagate
    }

    public enum ContentStreamAllowed
    {
        [CmisValue("notallowed")]
        NotAllowed,

        [CmisValue("allowed")]
        Allowed,

        [CmisValue("required")]
        Required
    }

    public enum PropertyType
    {
        [CmisValue("boolean")]
        Boolean,

        [CmisValue("id")]
        Id,

        [CmisValue("integer")]
        Integer,

        [CmisValue("datetime")]
        DateTime,

        [CmisValue("decimal")]
        Decimal,

        [CmisValue("html")]
        Html,

        [CmisValue("string")]
        String,

        [CmisValue("uri")]
        Uri
    }

    public enum Cardinality
    {
        [CmisValue("single")]
        Single,

        [CmisValue("multi")]
        Multi
    }

    public enum Updatability
    {
        [CmisValue("readonly")]
        ReadOnly,

        [CmisValue("readwrite")]
        ReadWrite,

        [CmisValue("whencheckedout")]
        WhenCheckedOut,

        [CmisValue("oncreate")]
        OnCreate
    }

    public enum DateTimeResolution
    {
        [CmisValue("year")]
        Year,

        [CmisValue("date")]
        Date,

        [CmisValue("time")]
        Time
    }

    public enum DecimalPrecision
    {
        [CmisValue("32")]
        Bits32,

        [CmisValue("64")]
        Bits64
    }

    public enum IncludeRelationshipsFlag
    {
        [CmisValue("none")]
        None,

        [CmisValue("source")]
        Source,

        [CmisValue("target")]
        Target,

        [CmisValue("both")]
        Both
    }

    public enum VersioningState
    {
        [CmisValue("none")]
        None,

        [CmisValue("major")]
        Major,

        [CmisValue("minor")]
        Minor,

        [CmisValue("checkedout")]
        CheckedOut
    }

    public enum UnfileObject
    {
        [CmisValue("unfile")]
        Unfile,

        [CmisValue("deletesinglefiled")]
        DeleteSinglefiled,

        [CmisValue("delete")]
        Delete
    }

    public enum RelationshipDirection
    {
        [CmisValue("source")]
        Source,

        [CmisValue("target")]
        Target,

        [CmisValue("either")]
        Either
    }

    public enum ReturnVersion
    {
        [CmisValue("this")]
        This,

        [CmisValue("latest")]
        Latest,

        [CmisValue("latestmajor")]
        LatestMajor
    }

    public enum ChangeType
    {
        [CmisValue("created")]
        Created,

        [CmisValue("updated")]
        Updated,

        [CmisValue("deleted")]
        Deleted,

        [CmisValue("security")]
        Security
    }

    // --- attribute class ---

    public class CmisValueAttribute : System.Attribute
    {
        public CmisValueAttribute(string value)
        {
            Value = value;
        }

        public string Value
        {
            get;
            private set;
        }
    }

    public static class CmisValue
    {
        private static IDictionary<Type, IDictionary<Enum, Enum>> CmisToSerializerDict = new Dictionary<Type, IDictionary<Enum, Enum>>();
        private static IDictionary<Type, IDictionary<Enum, Enum>> SerializerToCmisDict = new Dictionary<Type, IDictionary<Enum, Enum>>();

        static CmisValue()
        {
            MapEnums(typeof(BaseTypeId), typeof(enumBaseObjectTypeIds));
            MapEnums(typeof(CapabilityContentStreamUpdates), typeof(enumCapabilityContentStreamUpdates));
            MapEnums(typeof(CapabilityChanges), typeof(enumCapabilityChanges));
            MapEnums(typeof(CapabilityRenditions), typeof(enumCapabilityRendition));
            MapEnums(typeof(CapabilityQuery), typeof(enumCapabilityQuery));
            MapEnums(typeof(CapabilityJoin), typeof(enumCapabilityJoin));
            MapEnums(typeof(CapabilityAcl), typeof(enumCapabilityACL));
            MapEnums(typeof(SupportedPermissions), typeof(enumSupportedPermissions));
            MapEnums(typeof(AclPropagation), typeof(enumACLPropagation));
            MapEnums(typeof(ContentStreamAllowed), typeof(enumContentStreamAllowed));
            MapEnums(typeof(PropertyType), typeof(enumPropertyType));
            MapEnums(typeof(Cardinality), typeof(enumCardinality));
            MapEnums(typeof(Updatability), typeof(enumUpdatability));
            MapEnums(typeof(DateTimeResolution), typeof(enumDateTimeResolution));
            MapEnums(typeof(DecimalPrecision), typeof(enumDecimalPrecision));
            MapEnums(typeof(IncludeRelationshipsFlag), typeof(enumIncludeRelationships));
            MapEnums(typeof(ChangeType), typeof(enumTypeOfChanges));
        }

        private static void MapEnums(Type cmisEnum, Type xsdEnum)
        {
            IDictionary<Enum, Enum> cmisDict = new Dictionary<Enum, Enum>();
            CmisToSerializerDict[cmisEnum] = cmisDict;

            IDictionary<Enum, Enum> xsdDict = new Dictionary<Enum, Enum>();
            SerializerToCmisDict[xsdEnum] = xsdDict;

            foreach (FieldInfo xsdfieldInfo in xsdEnum.GetFields())
            {
                Enum xsdEnumValue = null;

                try
                {
                    xsdEnumValue = (Enum)Enum.Parse(xsdEnum, xsdfieldInfo.Name);
                }
                catch (Exception)
                {
                    continue;
                }

                XmlEnumAttribute[] xmlAttr = xsdfieldInfo.GetCustomAttributes(typeof(XmlEnumAttribute), false) as XmlEnumAttribute[];
                string value = xmlAttr.Length == 0 ? value = xsdfieldInfo.Name : value = xmlAttr[0].Name;

                foreach (FieldInfo cmisfieldInfo in cmisEnum.GetFields())
                {
                    CmisValueAttribute[] cmisValueAttr = cmisfieldInfo.GetCustomAttributes(typeof(CmisValueAttribute), false) as CmisValueAttribute[];
                    if (cmisValueAttr != null && cmisValueAttr.Length > 0 && cmisValueAttr[0].Value == value)
                    {
                        Enum cmisEnumValue = (Enum)Enum.Parse(cmisEnum, cmisfieldInfo.Name);
                        cmisDict[cmisEnumValue] = xsdEnumValue;
                        xsdDict[xsdEnumValue] = cmisEnumValue;
                        break;
                    }
                }
            }
        }

        public static Enum CmisToSerializerEnum(Enum source)
        {
            if (source == null) { return null; }

            Enum result = null;

            IDictionary<Enum, Enum> dict;
            if (CmisToSerializerDict.TryGetValue(source.GetType(), out dict))
            {
                dict.TryGetValue(source, out result);
            }

            if (result == null)
            {
                Console.WriteLine("*** " + source);
            }

            return result;
        }

        public static Enum SerializerToCmisEnum(Enum source)
        {
            if (source == null) { return null; }

            Enum result = null;

            IDictionary<Enum, Enum> dict;
            if (SerializerToCmisDict.TryGetValue(source.GetType(), out dict))
            {
                dict.TryGetValue(source, out result);
            }

            if (result == null)
            {
                Console.WriteLine("*** " + source);
            }

            return result;
        }

        public static string GetCmisValue(this Enum value)
        {
            FieldInfo fieldInfo = value.GetType().GetField(value.ToString());
            CmisValueAttribute[] cmisValueAttr = fieldInfo.GetCustomAttributes(typeof(CmisValueAttribute), false) as CmisValueAttribute[];

            return cmisValueAttr.Length > 0 ? cmisValueAttr[0].Value : null;
        }

        public static T GetCmisEnum<T>(this string value)
        {
            Type type = typeof(T);
            foreach (FieldInfo fieldInfo in type.GetFields())
            {
                CmisValueAttribute[] cmisValueAttr = fieldInfo.GetCustomAttributes(typeof(CmisValueAttribute), false) as CmisValueAttribute[];
                if (cmisValueAttr != null && cmisValueAttr.Length > 0 && cmisValueAttr[0].Value == value)
                {
                    return (T)Enum.Parse(type, fieldInfo.Name);
                }
            }

            return default(T);
        }
    }
}
