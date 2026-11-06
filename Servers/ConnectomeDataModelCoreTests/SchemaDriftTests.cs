using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Viking.DataModel.Annotation;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Offline schema drift check across the four places the annotation schema is written down:
    /// <c>Servers/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql</c> (what deployed databases have), the
    /// SSDT project (<c>Servers/AnnotationDatabase/dbo/Tables</c>), the smoke-test script
    /// (<c>scripts/minimal-schema.sql</c>) and the EF model (<see cref="AnnotationContext"/>). It compares
    /// table names, column names, column types and nullability, and checks that every object
    /// <c>minimal-schema.sql</c> creates also exists under <c>dbo/</c> and in <c>AnnotationDatabase.sqlproj</c>.
    /// No database is contacted: the SQL is read as text and the EF model is built without a connection.
    /// <para>
    /// <c>CreateUpdateDatabase.sql</c> is replayed rather than parsed once (see <see cref="ReplayUpdateScript"/>):
    /// its CREATE TABLE statements and its ALTER TABLE ... ADD, ALTER COLUMN and DROP COLUMN statements are applied
    /// in file order, which is DBVersion order, so the result is the final state of a database built by the script.
    /// </para>
    /// <para>
    /// Every difference must be on <see cref="AllowedDifferences"/>, each with a reason. An allow-list entry
    /// that no longer matches a difference also fails, so fixing the drift forces the entry to be removed.
    /// </para>
    /// <para>
    /// What the CREATE TABLE parser handles: <c>CREATE TABLE [dbo].[Name] ( ... )</c> (the <c>dbo.</c> and
    /// the brackets are optional), column names with or without brackets, one type per column (optionally
    /// <c>(n)</c>, <c>(max)</c> or <c>(p,s)</c>, optionally <c>[sys].</c>-qualified), and an explicit
    /// <c>NULL</c> or <c>NOT NULL</c> (a column without one is an error, since SQL Server's default depends on
    /// session settings). Computed columns (<c>AS expr [PERSISTED]</c>) compare by expression text with
    /// whitespace, brackets, parentheses and case removed, not by type, because SQL Server stores the
    /// expression re-bracketed and re-parenthesised; a change that only moves parentheses is not seen.
    /// Table-level constraints, defaults, identity, indexes, triggers, extended properties,
    /// statistics, the <c>graph</c> schema and the <c>UserActivity</c> view are not compared. <c>--</c> and
    /// <c>/* */</c> comments are removed before parsing, so a comment sign inside a string literal in a
    /// table definition would be misread. <c>float(53)</c> and <c>float</c> are the same type, and so are
    /// <c>timestamp</c> and <c>rowversion</c>.
    /// </para>
    /// </summary>
    public class SchemaDriftTests
    {
        private const string SqlProject = "sqlproj";
        private const string MinimalSchema = "minimal-schema";
        private const string EfModel = "EF";
        private const string UpdateScript = "CreateUpdateDatabase";

        /// <summary>Unroutable endpoint; building the model never opens a connection.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        /// <summary>
        /// Every difference the three sources are known to have, keyed by the text <see cref="Compare"/> reports,
        /// with the reason it is accepted. Keep a reason on every entry; do not add one to silence new drift.
        /// </summary>
        private static readonly Dictionary<string, string> AllowedDifferences = new()
        {
            // minimal-schema.sql is the smoke-test schema: it deliberately creates only the tables the
            // gRPC tests need, so the production-only tables are absent from it.
            [$"table dbo.DBVersion only in {SqlProject}, not in {MinimalSchema}"] =
                "Smoke schema omits the schema-version table; the EF model does not map it either.",
            [$"table dbo.StructureSpatialCache only in {SqlProject}, not in {MinimalSchema}"] =
                "Smoke schema omits the spatial cache (its trigger and functions are not created there).",
            [$"table dbo.StructureTemplates only in {SqlProject}, not in {MinimalSchema}"] =
                "Smoke schema omits the template table, which no gRPC service reads.",
            [$"table dbo.StructureSpatialCache only in {EfModel}, not in {MinimalSchema}"] =
                "EF maps the spatial cache; the smoke schema does not create it (see above).",
            [$"table dbo.StructureTemplates only in {EfModel}, not in {MinimalSchema}"] =
                "EF maps the template table; the smoke schema does not create it (see above).",

            // The EF model has no entity for the schema-version table.
            [$"table dbo.DBVersion only in {SqlProject}, not in {EfModel}"] =
                "No EF entity for dbo.DBVersion; the services never read it.",
            [$"table dbo.DBVersion only in {UpdateScript}, not in {EfModel}"] =
                "No EF entity for dbo.DBVersion (see above).",
            [$"table dbo.DBVersion only in {UpdateScript}, not in {MinimalSchema}"] =
                "Smoke schema omits the schema-version table (see above).",
            [$"table dbo.StructureSpatialCache only in {UpdateScript}, not in {MinimalSchema}"] =
                "Smoke schema omits the spatial cache (see above).",
            [$"table dbo.StructureTemplates only in {UpdateScript}, not in {MinimalSchema}"] =
                "Smoke schema omits the template table (see above).",
        };

        /// <summary>Objects minimal-schema.sql creates that are not in dbo/ or the sqlproj, with the reason.</summary>
        private static readonly Dictionary<string, string> AllowedMissingObjects = new();

        // ---- The checks -------------------------------------------------------------------------------

        [Fact]
        public void UpdateScriptSqlProjectMinimalSchemaAndEfModelAgreeOnTablesAndColumns()
        {
            var sources = new Dictionary<string, Dictionary<string, Column>>[]
            {
                ReplayUpdateScript(ReadUpdateScript()), ReadSqlProjectTables(), ParseCreateTables(ReadMinimalSchema()), ReadEfTables(),
            };
            var names = new[] { UpdateScript, SqlProject, MinimalSchema, EfModel };

            var found = new SortedSet<string>(StringComparer.Ordinal);
            for (var a = 0; a < sources.Length; a++)
                for (var b = a + 1; b < sources.Length; b++)
                    found.UnionWith(Compare(names[a], sources[a], names[b], sources[b]));

            AssertOnlyAllowed(found, AllowedDifferences, "schema difference");
        }

        [Fact]
        public void EveryObjectMinimalSchemaCreatesExistsInDboAndTheSqlProject()
        {
            var sqlprojPath = RepoFile("Servers/AnnotationDatabase/AnnotationDatabase.sqlproj");
            var projectDirectory = Path.GetDirectoryName(sqlprojPath);
            var missing = FindMissingObjects(
                ReadMinimalSchema(), File.ReadAllText(sqlprojPath), path => File.Exists(Path.Combine(projectDirectory, path)));

            AssertOnlyAllowed(new SortedSet<string>(missing.Keys, StringComparer.Ordinal),
                AllowedMissingObjects, "object missing from dbo/ or the sqlproj");
        }

        [Fact]
        public void AlterTableAddColumnsInMinimalSchemaMatchTheirCreateTable()
        {
            // minimal-schema.sql upgrades older test databases with ALTER TABLE ... ADD; a fresh database
            // gets the column from CREATE TABLE, so the two must say the same thing.
            var sql = ReadMinimalSchema();
            var tables = ParseCreateTables(sql);
            var added = AlterTableAddedColumns(sql).ToList();

            Assert.NotEmpty(added);
            foreach (var (table, column, definition) in added)
            {
                Assert.True(tables.TryGetValue(table, out var columns), $"ALTER TABLE names dbo.{table}, which has no CREATE TABLE");
                Assert.True(columns.TryGetValue(column, out var created), $"dbo.{table}.{column} is added by ALTER TABLE but not in CREATE TABLE");
                Assert.Equal(created, definition);
            }
        }

        [Fact]
        public void ParsersReadEveryTableOfTheRealSources()
        {
            // Guards against a parser that silently reads nothing, which would make the drift check vacuous.
            foreach (var tables in new[]
                     {
                         ReplayUpdateScript(ReadUpdateScript()), ReadSqlProjectTables(), ParseCreateTables(ReadMinimalSchema()), ReadEfTables(),
                     })
            {
                Assert.True(tables.Count >= 8, $"only {tables.Count} tables parsed");
                var location = tables["Location"];
                Assert.True(location.Count >= 20, $"dbo.Location parsed with {location.Count} columns");
                Assert.Equal(new Column("bigint", false), location["ParentID"]);
                Assert.Equal(new Column("geometry", false), location["MosaicShape"]);
                Assert.Equal(new Column("float", true), location["Width"]);
                Assert.StartsWith("computed ", location["Radius"].Type);
            }
        }

        // ---- Mutation checks: the comparison must see each kind of drift ------------------------------

        [Fact]
        public void CompareReportsADroppedColumn()
        {
            var original = ParseCreateTables(ReadMinimalSchema());
            var mutated = ParseCreateTables(MutateMinimalSchema("[Label]        VARCHAR (64) NULL,", ""));

            var differences = Compare(SqlProject, original, MinimalSchema, mutated).ToList();

            Assert.Contains($"column dbo.Structure.Label only in {SqlProject}, not in {MinimalSchema}", differences);
        }

        [Fact]
        public void CompareReportsAChangedNullability()
        {
            var original = ParseCreateTables(ReadMinimalSchema());
            var mutated = ParseCreateTables(MutateMinimalSchema(
                "[Notes]        NVARCHAR (MAX) NULL,", "[Notes]        NVARCHAR (MAX) NOT NULL,"));

            var differences = Compare(SqlProject, original, MinimalSchema, mutated).ToList();

            Assert.Contains($"column dbo.Structure.Notes: NULL in {SqlProject}, NOT NULL in {MinimalSchema}", differences);
        }

        [Fact]
        public void CompareReportsAChangedType()
        {
            var original = ParseCreateTables(ReadMinimalSchema());
            var mutated = ParseCreateTables(MutateMinimalSchema(
                "[Username]     NVARCHAR (254) CONSTRAINT [DF_Structure_Username]",
                "[Username]     NVARCHAR (100) CONSTRAINT [DF_Structure_Username]"));

            var differences = Compare(SqlProject, original, MinimalSchema, mutated).ToList();

            Assert.Contains($"column dbo.Structure.Username: nvarchar(254) in {SqlProject}, nvarchar(100) in {MinimalSchema}", differences);
        }

        [Fact]
        public void CompareReportsAChangedComputedColumn()
        {
            var original = ParseCreateTables(ReadMinimalSchema());
            var mutated = ParseCreateTables(MutateMinimalSchema(
                "sqrt([MosaicShape].[STArea]() / pi())", "sqrt([MosaicShape].[STArea]() / 3)"));

            var differences = Compare(SqlProject, original, MinimalSchema, mutated).ToList();

            Assert.Contains(differences, d => d.StartsWith("column dbo.Location.Radius:"));
        }

        [Fact]
        public void CompareReportsATableMissingFromOneSource()
        {
            var mutated = new Dictionary<string, Dictionary<string, Column>>(ParseCreateTables(ReadMinimalSchema()));
            mutated.Remove("LocationLink");

            var differences = Compare(SqlProject, ReadSqlProjectTables(), MinimalSchema, mutated).ToList();

            Assert.Contains($"table dbo.LocationLink only in {SqlProject}, not in {MinimalSchema}", differences);
        }

        [Fact]
        public void ObjectCheckReportsAnObjectMissingFromDboOrTheSqlProject()
        {
            const string sql = "CREATE TABLE [dbo].[Present] ([ID] BIGINT NOT NULL);\n" +
                               "EXEC(N'CREATE OR ALTER FUNCTION dbo.NoFile() RETURNS int AS BEGIN RETURN 1 END');\n" +
                               "CREATE TYPE [dbo].[NoProjEntry] AS TABLE ([ID] BIGINT NOT NULL);";
            const string sqlproj = "<Build Include=\"dbo\\Tables\\Present.sql\" /><Build Include=\"dbo\\Functions\\NoFile.sql\" />";

            var missing = FindMissingObjects(sql, sqlproj, path => !path.EndsWith("NoFile.sql"));

            Assert.Equal(
                new[] { "function NoFile", "type NoProjEntry" },
                missing.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void ParserRejectsAColumnWithoutExplicitNullability()
        {
            Assert.Throws<FormatException>(() => ParseCreateTables("CREATE TABLE [dbo].[T] ([ID] BIGINT);"));
        }

        [Fact]
        public void ParserReadsTypesNullabilityAndComputedColumns()
        {
            var tables = ParseCreateTables(
                "-- comment, with a comma\n" +
                "CREATE TABLE [dbo].[T] (\n" +
                "  [A] BIGINT IDENTITY (1, 1) NOT NULL,\n" +
                "  [B] NVARCHAR (MAX) NULL, /* c */\n" +
                "  [C] FLOAT (53) CONSTRAINT [DF_T_C] DEFAULT ((0.5)) NOT NULL,\n" +
                "  [D] [sys].[geometry] NOT NULL,\n" +
                "  [E] DECIMAL (18, 4) NULL,\n" +
                "  [F] AS (isnull([D].STX, (0))) PERSISTED NOT NULL,\n" +
                "  CONSTRAINT [PK_T] PRIMARY KEY CLUSTERED ([A] ASC));");

            var t = tables["T"];
            Assert.Equal(new[] { "A", "B", "C", "D", "E", "F" }, t.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(new Column("bigint", false), t["A"]);
            Assert.Equal(new Column("nvarchar(max)", true), t["B"]);
            Assert.Equal(new Column("float", false), t["C"]);
            Assert.Equal(new Column("geometry", false), t["D"]);
            Assert.Equal(new Column("decimal(18,4)", true), t["E"]);
            Assert.Equal(new Column("computed isnulld.stx,0", false), t["F"]);
        }

        [Fact]
        public void ReplayAppliesAlterTableStatementsInFileOrder()
        {
            var tables = ReplayUpdateScript(
                "CREATE TABLE [dbo].[T] ([A] [bigint] NOT NULL, [B] [ntext] NULL, [C] [float] NOT NULL, [V] [timestamp] NOT NULL)\n" +
                "CREATE TABLE U (ID bigint NOT NULL PRIMARY KEY CLUSTERED, Shape Geometry NOT NULL)\n" +
                "CREATE TABLE #Temp ([X] INT NULL)\n" +
                "\tALTER TABLE T ALTER COLUMN B NVARCHAR(max) NULL\n" +
                "\tALTER TABLE T ADD G geometry\n" +
                "\tALTER TABLE T ALTER COLUMN [G] geometry NOT NULL\n" +
                "\tALTER TABLE T DROP COLUMN [C]\n" +
                "\tALTER TABLE T ADD C as ISNULL(G.STX, 0) PERSISTED\n" +
                "\tALTER TABLE [dbo].[T] ADD CONSTRAINT [DF_T_A] DEFAULT ((0)) FOR [A]\n" +
                "\tEXEC(' ALTER TABLE T ADD R as\n\t\tCASE G.STDimension()\n\t\tWHEN 0 THEN 0\n\t\tEND PERSISTED NOT NULL ')\n" +
                "\tEXEC('ALTER TABLE [dbo].[U] ADD [Z] NVARCHAR (8) CONSTRAINT [DF_U_Z] DEFAULT (N''z'') NOT NULL')\n" +
                "\tALTER TABLE U ADD W float NULL\n\n");

            Assert.Equal(new[] { "T", "U" }, tables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            var t = tables["T"];
            Assert.Equal(new[] { "A", "B", "C", "G", "R", "V" }, t.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(new Column("nvarchar(max)", true), t["B"]);
            Assert.Equal(new Column("geometry", false), t["G"]);
            Assert.Equal(new Column("computed isnullg.stx,0", false), t["C"]);
            Assert.Equal(new Column("computed caseg.stdimensionwhen0then0end", false), t["R"]);
            Assert.Equal(new Column("rowversion", false), t["V"]);
            var u = tables["U"];
            Assert.Equal(new Column("geometry", false), u["Shape"]);
            Assert.Equal(new Column("nvarchar(8)", false), u["Z"]);
            Assert.Equal(new Column("float", true), u["W"]);
        }

        [Fact]
        public void ReplayAddsAColumnWithoutNullabilityAsNullable()
        {
            var tables = ReplayUpdateScript("CREATE TABLE T ([A] INT NOT NULL)\nALTER TABLE T ADD B geometry\n");

            Assert.Equal(new Column("geometry", true), tables["T"]["B"]);
        }

        [Fact]
        public void ReplayRejectsAnAlterOfAnUnknownTableOrColumn()
        {
            Assert.Throws<FormatException>(() => ReplayUpdateScript("ALTER TABLE T ADD B INT NULL\n"));
            Assert.Throws<FormatException>(() => ReplayUpdateScript("ALTER TABLE T ADD B INT NULL\nCREATE TABLE T ([A] INT NOT NULL)\n"));
            Assert.Throws<FormatException>(() => ReplayUpdateScript("CREATE TABLE T ([A] INT NOT NULL)\nALTER TABLE T DROP COLUMN B\n"));
            Assert.Throws<FormatException>(() => ReplayUpdateScript("CREATE TABLE T ([A] INT NOT NULL)\nALTER TABLE T ALTER COLUMN B INT NULL\n"));
            Assert.Throws<FormatException>(() => ReplayUpdateScript("CREATE TABLE T ([A] INT NOT NULL)\nALTER TABLE T ADD A INT NULL\n"));
        }

        [Fact]
        public void CompareReportsAnUpdateScriptStepThatChangesAColumn()
        {
            var script = ReadUpdateScript();
            const string step = "ALTER TABLE Location ADD Width float NULL";
            Assert.Single(Regex.Matches(script, Regex.Escape(step)));
            var mutated = ReplayUpdateScript(script.Replace(step, "ALTER TABLE Location ADD Width float NOT NULL"));

            var differences = Compare(UpdateScript, mutated, SqlProject, ReadSqlProjectTables()).ToList();

            Assert.Contains($"column dbo.Location.Width: NOT NULL in {UpdateScript}, NULL in {SqlProject}", differences);
        }

        /// <summary>The script text with one exact snippet replaced; fails if the snippet is not there once.</summary>
        private static string MutateMinimalSchema(string oldText, string newText)
        {
            var sql = ReadMinimalSchema();
            var first = sql.IndexOf(oldText, StringComparison.Ordinal);
            Assert.True(first >= 0 && sql.IndexOf(oldText, first + 1, StringComparison.Ordinal) < 0,
                "the mutation target must occur exactly once in minimal-schema.sql: " + oldText);
            return sql.Replace(oldText, newText);
        }

        // ---- Comparison -------------------------------------------------------------------------------

        /// <summary>One column as the drift check sees it: normalised type text and nullability.</summary>
        private sealed record Column(string Type, bool Nullable);

        private static string NullText(bool nullable) => nullable ? "NULL" : "NOT NULL";

        /// <summary>
        /// Lists the differences between two sources as stable sentences (these are the allow-list keys):
        /// tables present in one only, columns present in one only, and per-column type or nullability changes.
        /// Columns are compared only for tables both sources have.
        /// </summary>
        private static IEnumerable<string> Compare(
            string nameA, Dictionary<string, Dictionary<string, Column>> a,
            string nameB, Dictionary<string, Dictionary<string, Column>> b)
        {
            foreach (var table in a.Keys.Except(b.Keys))
                yield return $"table dbo.{table} only in {nameA}, not in {nameB}";
            foreach (var table in b.Keys.Except(a.Keys))
                yield return $"table dbo.{table} only in {nameB}, not in {nameA}";

            foreach (var table in a.Keys.Intersect(b.Keys))
            {
                var columnsA = a[table];
                var columnsB = b[table];
                foreach (var column in columnsA.Keys.Except(columnsB.Keys))
                    yield return $"column dbo.{table}.{column} only in {nameA}, not in {nameB}";
                foreach (var column in columnsB.Keys.Except(columnsA.Keys))
                    yield return $"column dbo.{table}.{column} only in {nameB}, not in {nameA}";

                foreach (var column in columnsA.Keys.Intersect(columnsB.Keys))
                {
                    var (typeA, nullA) = (columnsA[column].Type, columnsA[column].Nullable);
                    var (typeB, nullB) = (columnsB[column].Type, columnsB[column].Nullable);
                    if (typeA != typeB)
                        yield return $"column dbo.{table}.{column}: {typeA} in {nameA}, {typeB} in {nameB}";
                    if (nullA != nullB)
                        yield return $"column dbo.{table}.{column}: {NullText(nullA)} in {nameA}, {NullText(nullB)} in {nameB}";
                }
            }
        }

        /// <summary>Fails with the new and the stale differences listed, so one run shows everything to fix.</summary>
        private static void AssertOnlyAllowed(IReadOnlyCollection<string> found, Dictionary<string, string> allowed, string what)
        {
            Assert.All(allowed.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason), "every allow-list entry needs a reason"));

            var unexpected = found.Where(d => !allowed.ContainsKey(d)).ToList();
            var stale = allowed.Keys.Where(k => !found.Contains(k)).ToList();

            var message = new StringBuilder();
            if (unexpected.Count > 0)
                message.AppendLine($"Unexpected {what}(s); fix the drift or, with a reason, allow-list it:")
                       .AppendLine(string.Join(Environment.NewLine, unexpected.Select(d => "  + " + d)));
            if (stale.Count > 0)
                message.AppendLine($"Allow-listed {what}(s) that no longer occur; remove the entry:")
                       .AppendLine(string.Join(Environment.NewLine, stale.Select(d => "  - " + d)));
            Assert.True(message.Length == 0, message.ToString());
        }

        // ---- Objects created by minimal-schema.sql ---------------------------------------------------

        private static readonly Regex CreateObject = new(
            @"\bCREATE\s+(?:OR\s+ALTER\s+)?(?<kind>TABLE|PROCEDURE|PROC|FUNCTION|VIEW|TYPE)\s+\[?dbo\]?\.\[?(?<name>\w+)\]?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The <c>dbo/</c> folder (and so the sqlproj path) each kind of object lives in.</summary>
        private static string FolderFor(string kind) => kind switch
        {
            "table" => "Tables",
            "procedure" => "Stored Procedures",
            "function" => "Functions",
            "view" => "Views",
            "type" => "User Defined Types",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        /// <summary>
        /// Objects the script creates (also those created inside <c>EXEC(N'...')</c>) whose file is not under
        /// <c>dbo/</c> or not listed in the sqlproj. Keys read "kind Name"; values say what is missing.
        /// </summary>
        private static Dictionary<string, string> FindMissingObjects(string minimalSchemaSql, string sqlprojText, Func<string, bool> fileExists)
        {
            var missing = new Dictionary<string, string>();
            foreach (Match m in CreateObject.Matches(StripComments(minimalSchemaSql)))
            {
                var kind = m.Groups["kind"].Value.ToLowerInvariant();
                kind = kind == "proc" ? "procedure" : kind;
                var name = m.Groups["name"].Value;
                var relative = $"dbo/{FolderFor(kind)}/{name}.sql";

                var problems = new List<string>();
                if (!fileExists(relative)) problems.Add($"no file {relative}");
                if (sqlprojText.IndexOf($"Include=\"{relative.Replace('/', '\\')}\"", StringComparison.OrdinalIgnoreCase) < 0)
                    problems.Add("not listed in the sqlproj");
                if (problems.Count > 0)
                    missing[$"{kind} {name}"] = string.Join(", ", problems);
            }
            return missing;
        }

        // ---- Parsing the SQL text ---------------------------------------------------------------------

        private static readonly Regex CreateTableHead = new(
            @"\bCREATE\s+TABLE\s+(?:\[?dbo\]?\.)?\[?(?<name>\w+)\]?\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ColumnStart = new(
            @"^\s*(?:\[(?<name>[^\]]+)\]|(?<name>\w+))\s*(?<rest>.*)$", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        /// <summary>
        /// One column change in CreateUpdateDatabase.sql. The script ends statements at a line break, not with
        /// <c>;</c>, so a definition ends at the end of its line, or, for one run by <c>EXEC('...')</c>, at the
        /// quote that closes the string (doubled quotes inside it are skipped), since those may span lines.
        /// </summary>
        private static readonly Regex AlterTableColumn = new(
            @"(?<exec>\bEXEC\s*\(\s*N?'\s*)?\bALTER\s+TABLE\s+(?:\[?dbo\]?\.)?\[?(?<table>\w+)\]?\s+(?:WITH\s+(?:NO)?CHECK\s+)?" +
            @"(?<op>ADD|ALTER\s+COLUMN|DROP\s+COLUMN)\s+(?<body>(?(exec)(?:[^']|'')*|[^'\r\n;]*))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex TypeAtStart = new(
            @"^(?:\[sys\]\.)?\[?(?<type>\w+)\]?(?:\s*\(\s*(?<length>max|\d+)\s*(?:,\s*(?<scale>\d+)\s*)?\))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex AlterAddColumn = new(
            @"\bALTER\s+TABLE\s+dbo\.\[?(?<table>\w+)\]?\s+ADD\s+(?<column>\[\w+\]\s+.*?);",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly string[] TableLevelKeywords = { "CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "INDEX" };

        /// <summary>Parses every <c>CREATE TABLE [dbo].[Name] (...)</c> in the text into table name to columns.</summary>
        private static Dictionary<string, Dictionary<string, Column>> ParseCreateTables(string sql)
        {
            sql = StripComments(sql);
            var tables = new Dictionary<string, Dictionary<string, Column>>(StringComparer.Ordinal);
            foreach (Match head in CreateTableHead.Matches(sql))
                tables.Add(head.Groups["name"].Value, ParseTableBody(sql, head));
            return tables;
        }

        /// <summary>The columns of the CREATE TABLE whose head (ending at its opening parenthesis) is <paramref name="head"/>.</summary>
        private static Dictionary<string, Column> ParseTableBody(string sql, Match head)
        {
            var columns = new Dictionary<string, Column>(StringComparer.Ordinal);
            foreach (var item in SplitTopLevel(BalancedBody(sql, head.Index + head.Length - 1)))
            {
                if (IsTableLevelItem(item))
                    continue;
                var (name, column) = ParseColumn(item);
                columns.Add(name, column);
            }
            return columns;
        }

        private static bool IsTableLevelItem(string item) =>
            TableLevelKeywords.Any(k => item.TrimStart().StartsWith(k, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The final tables of a database built by CreateUpdateDatabase.sql: each CREATE TABLE sets a table and each
        /// ALTER TABLE ... ADD, ALTER COLUMN or DROP COLUMN changes one, in file order. Temporary (<c>#</c>) tables
        /// are skipped. A column added without NULL or NOT NULL is nullable, as on a connection with SQL Server's
        /// default ANSI_NULL_DFLT_ON; a computed column without one is NOT NULL when its outermost call is
        /// <c>ISNULL</c>, the only form the script uses, and nullable otherwise. Throws <see cref="FormatException"/>
        /// when a statement names a table not created earlier, or adds a column that exists or changes one that does not.
        /// </summary>
        private static Dictionary<string, Dictionary<string, Column>> ReplayUpdateScript(string sql)
        {
            sql = StripComments(sql);
            var tables = new Dictionary<string, Dictionary<string, Column>>(StringComparer.Ordinal);
            var statements = CreateTableHead.Matches(sql).Concat(AlterTableColumn.Matches(sql)).OrderBy(m => m.Index);
            foreach (var m in statements)
            {
                if (m.Groups["name"].Success)
                {
                    tables[m.Groups["name"].Value] = ParseTableBody(sql, m);
                    continue;
                }

                var table = m.Groups["table"].Value;
                var body = m.Groups["body"].Value;
                if (!tables.TryGetValue(table, out var columns))
                    throw new FormatException($"ALTER TABLE {table} before any CREATE TABLE {table}: {m.Value.Trim()}");
                var op = Regex.Replace(m.Groups["op"].Value, @"\s+", " ").ToUpperInvariant();
                if (op == "ADD" && IsTableLevelItem(body))
                    continue;

                var (name, column) = op == "DROP COLUMN" ? (body.Trim().Trim('[', ']'), (Column)null) : ParseScriptColumn(body);
                var exists = columns.ContainsKey(name);
                if (exists == (op == "ADD"))
                    throw new FormatException($"{op} on dbo.{table}.{name}, which {(exists ? "already exists" : "does not exist")}: {m.Value.Trim()}");
                if (column == null) columns.Remove(name);
                else columns[name] = column;
            }
            return tables;
        }

        /// <summary>A column definition from an ALTER TABLE in the update script; see <see cref="ReplayUpdateScript"/> for the nullability defaults.</summary>
        private static (string Name, Column Column) ParseScriptColumn(string item)
        {
            var (name, type, nullable) = ParseColumnParts(item);
            return (name, new Column(type, nullable ?? !type.StartsWith("computed isnull", StringComparison.Ordinal)));
        }

        /// <summary>Parses one column definition such as <c>[ID] BIGINT IDENTITY (1, 1) NOT NULL</c>.</summary>
        private static (string Name, Column Column) ParseColumn(string item)
        {
            var (name, type, nullable) = ParseColumnParts(item);
            if (nullable == null)
                throw new FormatException($"Column [{name}] has no explicit NULL or NOT NULL: {item.Trim()}");
            return (name, new Column(type, nullable.Value));
        }

        /// <summary>The name, normalised type text and explicit nullability (null when not stated) of one column definition.</summary>
        private static (string Name, string Type, bool? Nullable) ParseColumnParts(string item)
        {
            var start = ColumnStart.Match(item);
            if (!start.Success)
                throw new FormatException($"Unsupported table item (expected a column name): {item.Trim()}");
            var name = start.Groups["name"].Value;
            var rest = start.Groups["rest"].Value.Trim();
            var outsideParens = RemoveParenthesised(rest);

            bool? nullable = null;
            if (Regex.IsMatch(outsideParens, @"\bNOT\s+NULL\b", RegexOptions.IgnoreCase)) nullable = false;
            else if (Regex.IsMatch(outsideParens, @"\bNULL\b", RegexOptions.IgnoreCase)) nullable = true;

            if (Regex.IsMatch(rest, @"^AS\b", RegexOptions.IgnoreCase))
            {
                var expression = Regex.Replace(rest.Substring(2), @"(?:\s*\b(?:PERSISTED|NOT\s+NULL|NULL))+\s*$", "", RegexOptions.IgnoreCase);
                return (name, "computed " + NormaliseExpression(expression), nullable);
            }

            var type = TypeAtStart.Match(rest);
            if (!type.Success)
                throw new FormatException($"Column [{name}] has no recognisable type: {rest}");
            return (name, NormaliseType(type.Groups["type"].Value, type.Groups["length"].Value, type.Groups["scale"].Value), nullable);
        }

        /// <summary>
        /// The ALTER TABLE ... ADD [Column] definitions in the script, parsed like CREATE TABLE columns.
        /// A definition ends at the first <c>;</c>; the script writes one statement per line this way.
        /// </summary>
        private static IEnumerable<(string Table, string Column, Column Definition)> AlterTableAddedColumns(string sql)
        {
            foreach (Match m in AlterAddColumn.Matches(StripComments(sql)))
            {
                var (name, column) = ParseColumn(m.Groups["column"].Value);
                yield return (m.Groups["table"].Value, name, column);
            }
        }

        private static string NormaliseType(string type, string length, string scale)
        {
            type = type.ToLowerInvariant();
            if (type == "float") return "float";
            if (type == "timestamp") return "rowversion";
            var size = length.Length == 0 ? "" : scale.Length == 0 ? $"({length.ToLowerInvariant()})" : $"({length},{scale})";
            return type + size;
        }

        private static string NormaliseExpression(string expression) =>
            Regex.Replace(expression, @"[\s\[\]()]", "").ToLowerInvariant();

        internal static string StripComments(string sql) =>
            Regex.Replace(sql, @"/\*.*?\*/|--[^\r\n]*", " ", RegexOptions.Singleline);

        /// <summary>Text between the parenthesis at <paramref name="open"/> and its matching close parenthesis.</summary>
        private static string BalancedBody(string text, int open)
        {
            var depth = 0;
            for (var i = open; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')' && --depth == 0)
                    return text.Substring(open + 1, i - open - 1);
            }
            throw new FormatException("Unbalanced parentheses: " + text.Substring(open, Math.Min(80, text.Length - open)));
        }

        /// <summary>Splits on commas that are not inside parentheses.</summary>
        private static IEnumerable<string> SplitTopLevel(string body)
        {
            var depth = 0;
            var start = 0;
            for (var i = 0; i < body.Length; i++)
            {
                if (body[i] == '(') depth++;
                else if (body[i] == ')') depth--;
                else if (body[i] == ',' && depth == 0)
                {
                    yield return body.Substring(start, i - start);
                    start = i + 1;
                }
            }
            if (body.Substring(start).Trim().Length > 0)
                yield return body.Substring(start);
        }

        /// <summary>The text with every parenthesised group (nested ones included) removed.</summary>
        private static string RemoveParenthesised(string text)
        {
            var result = new StringBuilder();
            var depth = 0;
            foreach (var c in text)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (depth == 0) result.Append(c);
            }
            return result.ToString();
        }

        // ---- Reading the four sources -----------------------------------------------------------------

        private static string ReadMinimalSchema() => File.ReadAllText(RepoFile("Servers/AnnotationDatabase/scripts/minimal-schema.sql"));

        private static string ReadUpdateScript() => File.ReadAllText(RepoFile("Servers/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql"));

        private static Dictionary<string, Dictionary<string, Column>> ReadSqlProjectTables()
        {
            var directory = RepoFile("Servers/AnnotationDatabase/dbo/Tables");
            var tables = new Dictionary<string, Dictionary<string, Column>>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(directory, "*.sql"))
            {
                var parsed = ParseCreateTables(File.ReadAllText(file));
                var expected = Path.GetFileNameWithoutExtension(file);
                Assert.True(parsed.Count == 1 && parsed.ContainsKey(expected),
                    $"{file} should create exactly dbo.{expected}, found: {string.Join(", ", parsed.Keys)}");
                tables.Add(expected, parsed[expected]);
            }
            return tables;
        }

        /// <summary>The tables (not views) the EF model maps in the dbo schema, built offline.</summary>
        private static Dictionary<string, Dictionary<string, Column>> ReadEfTables()
        {
            var model = OfflineModel();
            var tables = new Dictionary<string, Dictionary<string, Column>>(StringComparer.Ordinal);
            foreach (var entity in model.GetEntityTypes())
            {
                var table = entity.GetTableName();
                // Keyless entities are the result shapes of stored procedures and functions, not tables.
                if (table == null || entity.FindPrimaryKey() == null || entity.GetViewName() != null || (entity.GetSchema() ?? "dbo") != "dbo")
                    continue;
                var store = StoreObjectIdentifier.Table(table, entity.GetSchema());

                var columns = new Dictionary<string, Column>(StringComparer.Ordinal);
                foreach (var property in entity.GetProperties())
                {
                    var name = property.GetColumnName(store);
                    if (name == null) continue;
                    var computed = property.GetComputedColumnSql(store);
                    var type = computed != null
                        ? "computed " + NormaliseExpression(computed)
                        : EfTypeText(property.GetColumnType(store));
                    columns.Add(name, new Column(type, property.IsColumnNullable(store)));
                }
                tables.Add(table, columns);
            }
            return tables;
        }

        /// <summary>The design-time <see cref="AnnotationContext"/> model, built without opening a connection.</summary>
        internal static IModel OfflineModel()
        {
            var options = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .Options;
            using var db = new AnnotationContext(options);
            return db.GetService<IDesignTimeModel>().Model;
        }

        /// <summary>EF store types read like "nvarchar(254)" or "float"; use the same spelling as the SQL parser.</summary>
        private static string EfTypeText(string storeType)
        {
            var m = TypeAtStart.Match(storeType);
            return NormaliseType(m.Groups["type"].Value, m.Groups["length"].Value, m.Groups["scale"].Value);
        }

        /// <summary>Finds a file in the repository by walking up from the test binaries.</summary>
        internal static string RepoFile(string relativePath)
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                    return candidate;
            }
            throw new FileNotFoundException("Not found above " + AppContext.BaseDirectory + ": " + relativePath);
        }
    }
}
