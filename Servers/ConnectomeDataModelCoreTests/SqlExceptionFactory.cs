using Microsoft.Data.SqlClient;
using System;
using System.Linq;
using System.Reflection;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Builds the <see cref="SqlException"/> SQL Server would raise, without a server: SqlClient keeps the constructors of
    /// <see cref="SqlError"/>, <see cref="SqlErrorCollection"/> and <see cref="SqlException"/> internal, so they are
    /// reached by reflection. SqlError's arguments are filled by parameter name, so a constructor that gains a parameter
    /// in a later SqlClient still matches.
    /// </summary>
    internal static class SqlExceptionFactory
    {
        private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        /// <summary>A SqlException holding one error per (number, message), in that order, as a THROW in dbo.SplitStructure raises it.</summary>
        public static SqlException Create(params (int Number, string Message)[] errors)
        {
            var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true);
            var add = typeof(SqlErrorCollection).GetMethod("Add", Internal);
            foreach (var (number, message) in errors)
                add.Invoke(collection, new object[] { Error(number, message) });

            var create = typeof(SqlException).GetMethods(Internal)
                .Single(m => m.Name == "CreateException" && m.GetParameters().Select(p => p.ParameterType)
                    .SequenceEqual(new[] { typeof(SqlErrorCollection), typeof(string) }));
            return (SqlException)create.Invoke(null, new object[] { collection, "16.0.0" });
        }

        private static SqlError Error(int number, string message)
        {
            var ctor = typeof(SqlError).GetConstructors(Internal)
                .Where(c => c.GetParameters().Any(p => p.Name == "errorMessage"))
                .OrderByDescending(c => c.GetParameters().Length)
                .First();
            var args = ctor.GetParameters().Select(p => p.Name switch
            {
                "infoNumber" => (object)number,
                "errorState" => (byte)1,
                "errorClass" => (byte)16,
                "server" => "test",
                "errorMessage" => message,
                "procedure" => "dbo.SplitStructure",
                "lineNumber" => 57,
                _ => p.HasDefaultValue ? p.DefaultValue : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null,
            }).ToArray();
            return (SqlError)ctor.Invoke(args);
        }
    }
}
