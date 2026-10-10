using System;
using System.Collections.Generic;

namespace Viking.Common
{
    /// <summary>
    /// Matches a structure id and/or label against a free-text query used by several tools
    /// (Review change feed, and any UI that filters by cell or label).
    /// Tokens are split on commas and whitespace; empty query matches everything.
    /// A token matches when it equals the structure id, or appears in the id or label (case-insensitive).
    /// </summary>
    public static class StructureOrLabelMatch
    {
        /// <summary>
        /// True when <paramref name="query"/> is empty, or any token matches
        /// <paramref name="structureId"/> / <paramref name="label"/>.
        /// </summary>
        public static bool Matches(long? structureId, string label, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string idText = structureId.HasValue ? structureId.Value.ToString() : "";
            string labelText = label ?? "";

            foreach (string token in Tokenize(query))
            {
                if (token.Length == 0)
                    continue;

                if (structureId.HasValue
                    && long.TryParse(token, out long id)
                    && id == structureId.Value)
                {
                    return true;
                }

                if (idText.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (labelText.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>Splits on commas and whitespace; empty tokens dropped.</summary>
        public static IReadOnlyList<string> Tokenize(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<string>();

            string[] parts = query.Split(
                new[] { ',', ' ', '\t', '\r', '\n', ';' },
                StringSplitOptions.RemoveEmptyEntries);
            return parts;
        }
    }
}
