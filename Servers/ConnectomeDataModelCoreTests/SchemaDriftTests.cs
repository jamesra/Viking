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
    /// Offline schema drift check across the three places the annotation schema is written down:
    /// the SSDT project (<c>Servers/AnnotationDatabase/dbo/Tables</c>), the smoke-test script
    /// (<c>scripts/minimal-schema.sql</c>) and the EF model (<see cref="AnnotationContext"/>). It compares
    /// table names, column names, column types and nullability, and checks that every object
    /// <c>minimal-schema.sql</c> creates also exists under <c>dbo/</c> and in <c>AnnotationDatabase.sqlproj</c>.
    /// No database is contacted: the SQL is read as text and the EF model is built without a connection.
    /// <para>
    /// Every difference must be on <see cref="AllowedDifferences"/>, each with a reason. An allow-list entry
    /// that no longer matches a difference also fails, so fixing the drift forces the entry to be removed.
    /// </para>
    /// <para>
    /// What the CREATE TABLE parser handles: <c>CREATE TABLE [dbo].[Name] ( ... )</c> with bracketed column
    /// names, one type per column (optionally <c>(n)</c>, <c>(max)</c> or <c>(p,s)</c>, optionally
    /// <c>[sys].</c>-qualified), and an explicit <c>NULL</c> or <c>NOT NULL</c> (a column without one is
    /// an error, since SQL Server's default depends on session settings). Computed columns
    /// (<c>AS (expr) [PERSISTED]</c>) compare by expression text with whitespace and case removed, not by
    /// type. Table-level constraints, defaults, identity, indexes, triggers, extended properties,
    /// statistics, the <c>graph</c> schema and the <c>UserActivity</c> view are not compared. <c>--</c> and
    /// <c>/* */</c> comments are removed before parsing, so a comment sign inside a string literal in a
    /// table definition would be misread. <c>float(53)</c> and <c>float</c> are the same type.
    /// </para>
    /// </summary>
    public class SchemaDriftTests
    {
        private const string SqlProject = "sqlproj";
        private const string MinimalSchema = "minimal-schema";
        private const string EfModel = "EF";

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
        };

        /// <summary>Objects minimal-schema.sql creates that are not in dbo/ or the sqlproj, with the reason.</summary>
        private static readonly Dictionary<string, string> AllowedMissingObjects = new();

        // ---- The checks -------------------------------------------------------------------------------

        [Fact]
        public void SqlProjectMinimalSchemaAndEfModelAgreeOnTablesAndColumns()
        {
            var sources = new Dictionary<string, Dictionary<string, Column>>[]
            {
                ReadSqlProjectTables(), ParseCreateTables(ReadMinimalSchema()), ReadEfTables(),
            };
            var names = new[] { SqlProject, MinimalSchema, EfModel };

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
            foreach (var tables in new[] { ReadSqlProjectTables(), ParseCreateTables(ReadMinimalSchema()), ReadEfTables() })
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
            Assert.Equal(new Column("computed isnull([d].stx,(0))", false), t["F"]);
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
            @"\bCREATE\s+TABLE\s+\[dbo\]\.\[(?<name>\w+)\]\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ColumnStart = new(
            @"^\s*\[(?<name>[^\]]+)\]\s*(?<rest>.*)$", RegexOptions.Singleline | RegexOptions.CultureInvariant);

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
            {
                var body = BalancedBody(sql, head.Index + head.Length - 1);
                var columns = new Dictionary<string, Column>(StringComparer.Ordinal);
                foreach (var item in SplitTopLevel(body))
                {
                    if (TableLevelKeywords.Any(k => item.TrimStart().StartsWith(k, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    var (name, column) = ParseColumn(item);
                    columns.Add(name, column);
                }
                tables.Add(head.Groups["name"].Value, columns);
            }
            return tables;
        }

        /// <summary>Parses one column definition such as <c>[ID] BIGINT IDENTITY (1, 1) NOT NULL</c>.</summary>
        private static (string Name, Column Column) ParseColumn(string item)
        {
            var start = ColumnStart.Match(item);
            if (!start.Success)
                throw new FormatException($"Unsupported table item (expected a bracketed column name): {item.Trim()}");
            var name = start.Groups["name"].Value;
            var rest = start.Groups["rest"].Value.Trim();
            var outsideParens = RemoveParenthesised(rest);

            bool nullable;
            if (Regex.IsMatch(outsideParens, @"\bNOT\s+NULL\b", RegexOptions.IgnoreCase)) nullable = false;
            else if (Regex.IsMatch(outsideParens, @"\bNULL\b", RegexOptions.IgnoreCase)) nullable = true;
            else throw new FormatException($"Column [{name}] has no explicit NULL or NOT NULL: {rest}");

            if (Regex.IsMatch(rest, @"^AS\b", RegexOptions.IgnoreCase))
            {
                var expression = BalancedBody(rest, rest.IndexOf('('));
                return (name, new Column("computed " + NormaliseExpression(expression), nullable));
            }

            var type = TypeAtStart.Match(rest);
            if (!type.Success)
                throw new FormatException($"Column [{name}] has no recognisable type: {rest}");
            return (name, new Column(NormaliseType(type.Groups["type"].Value, type.Groups["length"].Value, type.Groups["scale"].Value), nullable));
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
            var size = length.Length == 0 ? "" : scale.Length == 0 ? $"({length.ToLowerInvariant()})" : $"({length},{scale})";
            return type + size;
        }

        private static string NormaliseExpression(string expression) =>
            Regex.Replace(expression, @"\s+", "").ToLowerInvariant();

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

        // ---- Reading the three sources ----------------------------------------------------------------

        private static string ReadMinimalSchema() => File.ReadAllText(RepoFile("Servers/AnnotationDatabase/scripts/minimal-schema.sql"));

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
                        ? "computed " + NormaliseExpression(BalancedBody(computed, computed.IndexOf('(')))
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
