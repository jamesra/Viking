using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Service for managing debug logging configuration and runtime overrides
    /// Runtime overrides are per-process and do not persist across restarts or synchronize between services
    /// </summary>
    public class DebugLoggingService : IDebugLoggingService
    {
        private readonly DebugLoggingOptions _options;
        private readonly ConcurrentDictionary<DebugLogCategory, bool> _runtimeOverrides;
        private bool? _globalRuntimeOverride;
        private readonly object _lockObject = new object();

        public DebugLoggingService(IOptions<DebugLoggingOptions> options)
        {
            _options = options.Value ?? new DebugLoggingOptions();
            _runtimeOverrides = new ConcurrentDictionary<DebugLogCategory, bool>();
        }

        public bool IsEnabled(DebugLogCategory category)
        {
            // Check global runtime override first
            bool globalEnabled = _globalRuntimeOverride ?? _options.Enabled;

            // If global is disabled, nothing is enabled
            if (!globalEnabled)
                return false;

            // Check category-specific runtime override
            if (_runtimeOverrides.TryGetValue(category, out bool categoryOverride))
            {
                return categoryOverride;
            }

            // Check configuration
            return GetCategoryEnabled(category);
        }

        public void SetEnabled(DebugLogCategory category, bool enabled)
        {
            lock (_lockObject)
            {
                _runtimeOverrides.AddOrUpdate(category, enabled, (key, oldValue) => enabled);
            }
        }

        public void SetGlobalEnabled(bool enabled)
        {
            lock (_lockObject)
            {
                _globalRuntimeOverride = enabled;
            }
        }

        public DebugLoggingOptions GetOptions()
        {
            var result = new DebugLoggingOptions
            {
                Enabled = _globalRuntimeOverride ?? _options.Enabled,
                Categories = new DebugCategoryOptions
                {
                    Authentication = GetCategoryEnabled(DebugLogCategory.Authentication),
                    Permissions = GetCategoryEnabled(DebugLogCategory.Permissions),
                    Database = GetCategoryEnabled(DebugLogCategory.Database),
                    Email = GetCategoryEnabled(DebugLogCategory.Email),
                    Api = GetCategoryEnabled(DebugLogCategory.Api),
                    Configuration = GetCategoryEnabled(DebugLogCategory.Configuration)
                }
            };

            return result;
        }

        public void ResetToConfiguration()
        {
            lock (_lockObject)
            {
                _globalRuntimeOverride = null;
                _runtimeOverrides.Clear();
            }
        }

        private bool GetCategoryEnabled(DebugLogCategory category)
        {
            return category switch
            {
                DebugLogCategory.Authentication => _options.Categories.Authentication,
                DebugLogCategory.Permissions => _options.Categories.Permissions,
                DebugLogCategory.Database => _options.Categories.Database,
                DebugLogCategory.Email => _options.Categories.Email,
                DebugLogCategory.Api => _options.Categories.Api,
                DebugLogCategory.Configuration => _options.Categories.Configuration,
                _ => false
            };
        }
    }
}


