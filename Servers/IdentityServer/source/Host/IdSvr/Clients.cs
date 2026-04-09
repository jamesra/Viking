/*
 * Copyright 2014 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using Thinktecture.IdentityServer.Core;
using Thinktecture.IdentityServer.Core.Models;
using System.Web.Configuration; 

namespace VikingIdentityServer
{
    public class Clients
    {
        public static List<Client> Get()
        {
            string IdentityManagerUri = WebConfigurationManager.AppSettings.Get("IdentityManagerUri");
            string VikingClientSecret = WebConfigurationManager.AppSettings.Get("VikingClientSecret");

            return new List<Client>
            {
                new Client
                {
                    ClientName = "Viking",
                    ClientId = "Viking",
                    Enabled = true, 
                    AccessTokenType = Thinktecture.IdentityServer.Core.Models.AccessTokenType.Reference,
                    ClientSecrets = new List<ClientSecret>{
                        new ClientSecret(VikingClientSecret.Sha256())
                    },
                    Flow = Flows.ResourceOwner
                },
                new Client{
                    ClientId = "idmgr_client",
                    ClientName = "IdentityManager",
                    Enabled = true,
                    Flow = Flows.Implicit,
                    RequireConsent = false,
                    RedirectUris = new List<string>{
                        IdentityManagerUri,
                    },
                    IdentityProviderRestrictions = new List<string>(){Thinktecture.IdentityServer.Core.Constants.PrimaryAuthenticationType}
                }
            };
        }
    }
}