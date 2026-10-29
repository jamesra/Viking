using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Every trigger a deployed annotation database has must be declared in the EF model. SQL Server
    /// rejects <c>INSERT/UPDATE/DELETE ... OUTPUT</c> on a table with an enabled trigger (Msg 334), and EF
    /// Core only avoids that clause for tables whose triggers the model declares.
    /// <para>
    /// The triggers a deployed database has are the ones <c>CreateUpdateDatabase.sql</c> leaves after every
    /// step: its <c>CREATE</c>, <c>ALTER</c> and <c>DROP TRIGGER</c> statements replayed in file order. That set
    /// must equal the triggers the sqlproj (<c>Servers/AnnotationDatabase/dbo</c>) creates and the triggers
    /// the model declares, in both directions: a trigger the model misses breaks writes, and a declared
    /// trigger no database has hides a stale entry. No database is contacted.
    /// </para>
    /// <para>
    /// What the parser handles: <c>CREATE [OR ALTER] TRIGGER</c>, <c>ALTER TRIGGER</c> and <c>DROP TRIGGER [IF EXISTS]</c>
    /// with or without brackets and the <c>dbo.</c> prefix, also inside <c>EXEC('...')</c>. Comments are
    /// removed first, so a trigger mentioned in a comment does not count.
    /// </para>
    /// </summary>
    public class ModelTriggerDeclarationTests
    {
        private const string UpgradeScriptPath = "Servers/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql";

        private static readonly Regex TriggerStatement = new(
            @"\b(?:(?<create>CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+TRIGGER\s+(?:\[?dbo\]?\.)?\[?(?<name>\w+)\]?\s+ON\s+(?:\[?dbo\]?\.)?\[?(?<table>\w+)\]?" +
            @"|DROP\s+TRIGGER\s+(?:IF\s+EXISTS\s+)?(?:\[?dbo\]?\.)?\[?(?<dropped>\w+)\]?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        [Fact]
        public void ModelDeclaresExactlyTheTriggersTheUpgradeScriptLeaves()
        {
            AssertSameTriggers("CreateUpdateDatabase.sql", ReplayTriggers(ReadUpgradeScript()), "EF model", DeclaredTriggers(SchemaDriftTests.OfflineModel()));
        }

        [Fact]
        public void SqlProjectCreatesExactlyTheTriggersTheUpgradeScriptLeaves()
        {
            AssertSameTriggers("CreateUpdateDatabase.sql", ReplayTriggers(ReadUpgradeScript()), "sqlproj", ReadSqlProjectTriggers());
        }

        [Fact]
        public void ParserFindsEveryTriggerOfTheRealScript()
        {
            // Guards against a parser that silently reads nothing, which would make the checks above vacuous.
            Assert.Equal(
                new[]
                {
                    "Location.Location_delete", "Location.Location_update", "Location.UpdateStructureSpatialCache",
                    "Structure.Structure_LastModified", "Structure.Structure_delete",
                    "StructureLink.StructureLink_ReciprocalCheck", "StructureType.StructureType_LastModified",
                },
                ReplayTriggers(ReadUpgradeScript()).ToArray());
        }

        // ---- Mutation checks: the comparison must see each kind of drift ------------------------------

        [Fact]
        public void ATriggerAddedByALaterStepFailsUntilTheModelDeclaresIt()
        {
            var script = ReadUpgradeScript() +
                         "\nEXEC('CREATE TRIGGER StructureLink_Audit ON StructureLink AFTER DELETE AS SET NOCOUNT ON;');\n";

            var differences = Differences("script", ReplayTriggers(script), "EF", DeclaredTriggers(SchemaDriftTests.OfflineModel()));

            Assert.Equal(new[] { "StructureLink.StructureLink_Audit only in script, not in EF" }, differences);
        }

        [Fact]
        public void ATriggerDroppedByALaterStepFailsWhileTheModelStillDeclaresIt()
        {
            var script = ReadUpgradeScript() +
                         "\nIF EXISTS(select * from sys.triggers where name = 'Location_update')\n    DROP TRIGGER [dbo].[Location_update];\n";

            var differences = Differences("script", ReplayTriggers(script), "EF", DeclaredTriggers(SchemaDriftTests.OfflineModel()));

            Assert.Equal(new[] { "Location.Location_update only in EF, not in script" }, differences);
        }

        [Fact]
        public void ReplayKeepsFileOrderAndIgnoresComments()
        {
            const string script =
                "CREATE TRIGGER [dbo].[A_t] ON [dbo].[A] FOR UPDATE AS SET NOCOUNT ON;\n" +
                "DROP TRIGGER [dbo].[A_t];\n" +
                "EXEC('CREATE OR ALTER TRIGGER dbo.B_t ON dbo.B AFTER INSERT AS SET NOCOUNT ON;');\n" +
                "DROP TRIGGER IF EXISTS B_t;\n" +
                "ALTER TRIGGER B_t ON B AFTER INSERT, UPDATE AS SET NOCOUNT ON;\n" +
                "-- CREATE TRIGGER [dbo].[C_t] ON [dbo].[C] FOR DELETE\n" +
                "/* CREATE TRIGGER D_t ON D FOR DELETE */";

            Assert.Equal(new[] { "B.B_t" }, ReplayTriggers(script).ToArray());
        }

        // ---- Sources and comparison -------------------------------------------------------------------

        /// <summary>
        /// The triggers left after running every CREATE/ALTER/DROP TRIGGER in the text in order, as
        /// "Table.Trigger". A DROP removes the trigger by name, whatever table it was on.
        /// </summary>
        private static SortedSet<string> ReplayTriggers(string sql)
        {
            var tableOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in TriggerStatement.Matches(SchemaDriftTests.StripComments(sql)))
            {
                if (m.Groups["create"].Success)
                    tableOf[m.Groups["name"].Value] = m.Groups["table"].Value;
                else
                    tableOf.Remove(m.Groups["dropped"].Value);
            }
            return new SortedSet<string>(tableOf.Select(t => $"{t.Value}.{t.Key}"), StringComparer.Ordinal);
        }

        /// <summary>The triggers the sqlproj's <c>dbo</c> files create.</summary>
        private static SortedSet<string> ReadSqlProjectTriggers()
        {
            var directory = SchemaDriftTests.RepoFile("Servers/AnnotationDatabase/dbo");
            var all = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(directory, "*.sql", SearchOption.AllDirectories))
                all.UnionWith(ReplayTriggers(File.ReadAllText(file)));
            return all;
        }

        /// <summary>The triggers the EF model declares on each mapped table, as "Table.Trigger".</summary>
        private static SortedSet<string> DeclaredTriggers(IModel model) => new(
            model.GetEntityTypes().SelectMany(entity => entity.GetDeclaredTriggers()
                .Select(trigger => $"{trigger.GetTableName() ?? entity.GetTableName()}.{trigger.GetDatabaseName() ?? trigger.ModelName}")),
            StringComparer.Ordinal);

        private static string ReadUpgradeScript() => File.ReadAllText(SchemaDriftTests.RepoFile(UpgradeScriptPath));

        private static List<string> Differences(string nameA, SortedSet<string> a, string nameB, SortedSet<string> b) =>
            a.Except(b).Select(t => $"{t} only in {nameA}, not in {nameB}")
             .Concat(b.Except(a).Select(t => $"{t} only in {nameB}, not in {nameA}"))
             .ToList();

        private static void AssertSameTriggers(string nameA, SortedSet<string> a, string nameB, SortedSet<string> b)
        {
            var differences = Differences(nameA, a, nameB, b);
            Assert.True(differences.Count == 0,
                "Trigger sets differ; declare each trigger with ToTable(tb => tb.HasTrigger(...)) in " +
                "AnnotationContext_UserDefined.cs and keep the sqlproj in step with the script:" +
                Environment.NewLine + string.Join(Environment.NewLine, differences));
        }
    }
}
