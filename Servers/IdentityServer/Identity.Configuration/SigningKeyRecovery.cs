using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Viking.Identity.Server
{
    /// <summary>
    /// Drops IdentityServer signing keys that can no longer be unwrapped and asks Duende to
    /// create a replacement. Called from Standalone startup after the persisted-grant database
    /// is migrated, before the host listens.
    /// </summary>
    public static class SigningKeyRecovery
    {
        /// <summary>
        /// Loads every stored signing key, deletes those whose data-protection key is missing
        /// from the ring, then ensures a current signing key exists. Readable keys are left
        /// in place. Tokens signed by a deleted key stop validating and clients must sign in again.
        /// </summary>
        /// <param name="services">A scope that can resolve the operational signing-key store.</param>
        public static void RemoveUnreadableSigningKeys(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);
            // Startup may have a sync context; don't block it while the store awaits SQL.
            Task.Run(() => RemoveUnreadableSigningKeysAsync(services)).GetAwaiter().GetResult();
        }

        private static async Task RemoveUnreadableSigningKeysAsync(IServiceProvider services)
        {
            var store = services.GetService<ISigningKeyStore>();
            var protector = services.GetService<ISigningKeyProtector>();
            if (store == null || protector == null)
            {
                Log.Information("Signing-key recovery skipped; key management is not registered.");
                return;
            }

            var keys = await store.LoadKeysAsync().ConfigureAwait(false);
            if (keys == null)
            {
                return;
            }

            var removed = 0;
            foreach (var key in keys)
            {
                if (key == null || string.IsNullOrEmpty(key.Id))
                {
                    continue;
                }

                try
                {
                    protector.Unprotect(key);
                }
                catch (Exception ex) when (IsMissingDataProtectionKey(ex))
                {
                    Log.Warning(
                        ex,
                        "Deleting IdentityServer signing key {Kid} because its data-protection key is not in the key ring.",
                        key.Id);
                    await store.DeleteKeyAsync(key.Id).ConfigureAwait(false);
                    removed++;
                }
            }

            if (removed == 0)
            {
                return;
            }

            var manager = services.GetRequiredService<IKeyManager>();
            await manager.GetCurrentKeysAsync().ConfigureAwait(false);
            Log.Warning(
                "Removed {Count} unreadable IdentityServer signing key(s) and loaded a current signing key. Clients that presented a token from a removed key must sign in again.",
                removed);
        }

        /// <summary>
        /// True when unprotect failed because the ASP.NET data-protection key is absent.
        /// Other failures are left for the caller so a bug cannot wipe the key table.
        /// </summary>
        private static bool IsMissingDataProtectionKey(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is CryptographicException)
                {
                    return true;
                }

                if (current is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                    {
                        if (IsMissingDataProtectionKey(inner))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
