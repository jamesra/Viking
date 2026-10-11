using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Maps Identity volume Name to an annotation SQL connection string.
    /// Named map wins; otherwise AnnotationConnectionTemplate may contain {VolumeName}.
    /// </summary>
    public sealed class AnnotationConnectionResolver
    {
        readonly Dictionary<string, string> _map;
        readonly string _template;

        public AnnotationConnectionResolver(IReadOnlyDictionary<string, string> map, string template)
        {
            _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (map != null)
            {
                foreach (KeyValuePair<string, string> kv in map)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                        _map[kv.Key] = kv.Value;
                }
            }

            _template = template;
        }

        public static AnnotationConnectionResolver FromConfiguration(IConfiguration configuration)
        {
            Dictionary<string, string> map = [];
            IConfigurationSection section = configuration.GetSection("AnnotationConnections");
            foreach (IConfigurationSection child in section.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Key) && !string.IsNullOrWhiteSpace(child.Value))
                    map[child.Key] = child.Value;
            }

            return new AnnotationConnectionResolver(map, configuration["AnnotationConnectionTemplate"]);
        }

        /// <summary>
        /// Resolves the connection for a correction set: the annotation database name first, then each context name
        /// that maps to it, so existing per-volume configuration keeps working.
        /// </summary>
        /// <param name="database">Annotation database name (the correction-set key).</param>
        /// <param name="contextNames">Names of the contexts that share the database.</param>
        /// <returns>A connection string, or null when nothing is configured.</returns>
        public string Resolve(string database, IEnumerable<string> contextNames)
        {
            string connection = Resolve(database);
            if (!string.IsNullOrWhiteSpace(connection))
                return connection;
            foreach (string name in contextNames)
            {
                connection = Resolve(name);
                if (!string.IsNullOrWhiteSpace(connection))
                    return connection;
            }

            return null;
        }

        public string Resolve(string volumeName)
        {
            if (string.IsNullOrWhiteSpace(volumeName))
                return null;
            if (_map.TryGetValue(volumeName, out string mapped) && !string.IsNullOrWhiteSpace(mapped))
                return mapped;
            if (string.IsNullOrWhiteSpace(_template))
                return null;
            return _template.Replace("{VolumeName}", volumeName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
