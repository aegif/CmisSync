using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS;
using DotCMIS.Exceptions;

namespace SparkleLib.Cmis
{
    public class CmisUtils
    {
        static public string[] GetRepositories(string url, string user, string password)
        {
            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();

            Dictionary<string, string> cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] =password;

            IList<IRepository> repositories;
            try
            {
                repositories = factory.GetRepositories(cmisParameters);
            }
            catch (CmisPermissionDeniedException e)
            {
                throw new CmisServerNotFoundException("CMIS server found, but permission denied. Please check username/password");
            }
            catch (CmisRuntimeException e)
            {
                // TODO try harder by extracting the hostname and changing the URL to well-known patterns
                throw new CmisServerNotFoundException("Sorry, CmisSync can not find a CMIS server at this address.\nPlease check again.\nIf you are sure about the address, open it in a browser and post\nthe resulting XML to the CmisSync forum.");
            }

            string[] result = new string[repositories.Count];

            for (int i = 0; i < repositories.Count; i++)
            {
                result[i] = repositories.ElementAt(i).Id; // TODO displaying Name would be more user-friendly than Id
            }
            
            return result;
        }

        static public string[] getSubfolders(string repositoryId, string path,
            string address, string user, string password)
        {
            List<string> result = new List<string>();

            Dictionary<string, string> cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = address;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] = password;
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.CreateSession(cmisParameters);

            IFolder folder = (IFolder)session.GetObjectByPath("/" + path);
            foreach (ICmisObject obj in folder.GetChildren())
            {
                if (obj is IFolder)
                    result.Add(obj.Name);
            }


            return result.ToArray();//new string[] { "bob", "nick" };
        }
    }
}
