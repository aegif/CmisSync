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
                if (tokenDict.ContainsKey(user))
                {
                    return tokenDict[user];
                }
                else
                {
                    return null;
                }
            }
            public void put(string user, TokenObject tokenObject)
            {
                if (tokenDict.ContainsKey(user))
                {
                    tokenDict[user] = tokenObject;
                }
                else
                {
                    tokenDict.Add(user, tokenObject);
                }
            }
        }

        public class TokenObject
        {
            public TokenObject(string token, long expiration)
            {
                this.token = token;
                this.expiration = expiration;
            }

            public string token { get; set; }
            public long expiration { get; set; }
        }

        public static void put(string repositoryId, string user, TokenObject tokenObject)
        {
            if (!store.ContainsKey(repositoryId))
            {
                store.Add(repositoryId, new RepositoryStore(repositoryId));
            }

            store[repositoryId].put(user, tokenObject);
        }

        public static TokenObject get(string repositoryId, string user)
        {
            if (store.ContainsKey(repositoryId))
            {
                RepositoryStore repoStore = store[repositoryId];
                return repoStore.get(user);
            }
            
            return null;
        }
    }
}
