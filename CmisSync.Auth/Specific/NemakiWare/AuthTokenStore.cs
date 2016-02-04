using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Auth.Specific.NemakiWare
{
    class AuthTokenStore
    {
        private static Dictionary<string, RepositoryStore> store = new Dictionary<string, RepositoryStore>();

        public class RepositoryStore
        {
            private string repositoryId;
            private Dictionary<string, TokenObject> tokenDict = new Dictionary<string, TokenObject>();

            public RepositoryStore(string repositoryId)
            {
                this.repositoryId = repositoryId;
            }

            public TokenObject get(string user)
            {
                return tokenDict[user];
            }
            public void put(string user, TokenObject tokenObject)
            {
                tokenDict[user] = tokenObject;
            }
        }

        public class TokenObject
        {
            public string token { get; set; }
            public long expiration { get; set; }


        }

        public static void put(string repositoryId, string user, TokenObject tokenObject)
        {
            RepositoryStore repoStore = store[repositoryId];
            if(repoStore == null)
            {
                store[repositoryId] = new RepositoryStore(repositoryId);
                repoStore = store[repositoryId];
            }

            repoStore.put(user, tokenObject);
        }

        public static TokenObject get(string repositoryId, string user)
        {
            RepositoryStore repoStore = store[repositoryId];
            if(repoStore != null)
            {
                return repoStore.get(user);
            }
            
            return null;
        }
    }
}
