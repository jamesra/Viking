﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using IdentityServer4.Configuration;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using IdentityServer.Data;
using IdentityModel;
using IdentityServer4;

namespace IdentityServer
{
    public class IdentityServerVikingClientStore : IClientStore
    {
        ApplicationDbContext _context;
        IResourceStore _resourceStore;

        public IdentityServerVikingClientStore(ApplicationDbContext context, IResourceStore resourceStore)
        {
            _context = context;
            _resourceStore = resourceStore;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId != "ro.viking")
                return null;

            var allResources = await _resourceStore.GetAllResourcesAsync();

            var scopes = allResources.ApiScopes.Select(s => s.Name).ToList();

            scopes.Add(IdentityServerConstants.StandardScopes.OpenId);
            scopes.Add(IdentityServerConstants.StandardScopes.Profile);
            scopes.AddRange(IdentityServerCustomResourceStore.StandardScopes.Select(s => s.Name));

            var result = new Client
            {
                ClientId = "ro.viking",
                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                ClientSecrets =
                    {
                        new Secret(Config.Secret.Sha256())
                    },
                AllowedScopes = scopes,
            };

            return result;
        }
    }
}
