using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Viking.Identity.Data
{
    public class EFModelAnalysis
    {
        readonly DbContext _context;
        public EFModelAnalysis(DbContext context)
        {
            Contract.Requires(context != null);
            _context = context;
        }

        public List<string> ShadowProperties()
        {
            List<string> results = new List<string>();

            var entityTypes = _context.Model.GetEntityTypes();
            foreach (var entityType in entityTypes)
            {
                var entityProperties = entityType.GetProperties();
                foreach (var entityProperty in entityProperties)
                {
                    if (entityProperty.IsShadowProperty())
                    {
                        string output = $"{entityType.Name}.{entityProperty.Name}: {entityProperty}.";
                        results.Add(output);
                    }
                }
            }
            return results;
        }

        public List<string> AlternateKeyRelationships()
        {
            List<string> results = new List<string>();

            var entityTypes = _context.Model.GetEntityTypes();
            foreach (var entityType in entityTypes)
            {
                foreach (var fk in entityType.GetForeignKeys())
                {
                    if (!fk.PrincipalKey.IsPrimaryKey())
                    {
                        string output = $"{entityType.DisplayName()} Foreign Key {fk.GetConstraintName()} " +
                            $"references principal ALTERNATE key {fk.PrincipalKey} " +
                            $"in table {fk.PrincipalEntityType}.";
                        results.Add(output);
                    }
                }
            }
            return results;
        }
    }
}
