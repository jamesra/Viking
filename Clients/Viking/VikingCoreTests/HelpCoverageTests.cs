using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VikingCoreTests
{
    /// <summary>
    /// Source guards for the help bar (<c>.cursor/rules/help-entries.mdc</c>). Keys must be handled through a
    /// <c>KeyBinding</c> table, which documents them; mouse and pen gestures are documented by hand, so a class that really
    /// handles the wheel, a double-click or pen contact must list that input in its help (or its base class's).
    /// </summary>
    [TestClass]
    public class HelpCoverageTests
    {
        private static readonly string[] Folders =
        [
            "Clients/Viking/WebAnnotation/UI",
            "Clients/Viking/WebAnnotation/View",
            "Clients/Viking/NGVV/UI/Command",
            "Clients/Viking/NGVV/UI/Controls/SectionViewerControl.cs",
            "Clients/Viking/MeasurementExtension",
            "Clients/Viking/LocalBookmarks",
        ];

        /// <summary>Handler name, then the help inputs one of which must appear when the handler does something.</summary>
        private static readonly (string Handler, string[] Inputs)[] Gestures =
        [
            ("OnMouseWheel", ["HelpInput.Wheel"]),
            ("OnMouseHWheel", ["HelpInput.TiltWheel"]),
            ("OnMouseDoubleClick", ["HelpInput.LeftDoubleClick", "HelpInput.RightDoubleClick"]),
            ("OnPenContact", ["HelpInput.Pen", "HelpInput.PenContact"]),
        ];

        private static Dictionary<string, string> Sources()
        {
            string root = ChangelogTests.FindRepositoryRoot();
            IEnumerable<string> files = Folders.Select(f => Path.Combine(root, f)).SelectMany(p =>
                File.Exists(p) ? [p] : Directory.Exists(p) ? Directory.EnumerateFiles(p, "*.cs", SearchOption.AllDirectories) : []);
            return files.Where(f => !f.EndsWith(".Designer.cs")).ToDictionary(f => f.Substring(root.Length + 1).Replace('\\', '/'), File.ReadAllText);
        }

        /// <summary>The body of each override of <paramref name="handler"/> in <paramref name="source"/>, braces excluded.</summary>
        private static IEnumerable<string> OverrideBodies(string source, string handler)
        {
            foreach (Match m in Regex.Matches(source, @"override\s+void\s+" + handler + @"\s*\([^)]*\)\s*\{"))
            {
                int depth = 1, i = m.Index + m.Length;
                int start = i;
                for (; i < source.Length && depth > 0; i++)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}') depth--;
                }

                yield return source.Substring(start, i - 1 - start);
            }
        }

        /// <summary>True when a body does more than call the base handler (comments ignored).</summary>
        private static bool DoesSomething(string body)
        {
            string code = Regex.Replace(body, @"//[^\n]*|/\*.*?\*/", "", RegexOptions.Singleline);
            code = Regex.Replace(code, @"base\.\w+\([^;]*\);|return;", "");
            return code.Trim().Length > 0;
        }

        /// <summary>The source of the file declaring <paramref name="source"/>'s classes' base classes, transitively.</summary>
        private static string WithBaseClasses(string source, Dictionary<string, string> all, HashSet<string>? seen = null)
        {
            seen ??= [];
            string text = source;
            foreach (Match m in Regex.Matches(source, @"class\s+\w+(?:<[^>]*>)?\s*:\s*([\w.]+)"))
            {
                string baseName = m.Groups[1].Value.Split('.').Last();
                if (!seen.Add(baseName))
                    continue;

                foreach (string other in all.Values.Where(o => Regex.IsMatch(o, @"class\s+" + baseName + @"\b")))
                    text += WithBaseClasses(other, all, seen);
            }

            return text;
        }

        [TestMethod]
        public void MouseAndPenHandlersListTheirInputs()
        {
            Dictionary<string, string> sources = Sources();
            Assert.IsTrue(sources.Count > 20, "the command sources were not found");

            List<string> missing = [];
            foreach (KeyValuePair<string, string> pair in sources)
            {
                (string file, string source) = (pair.Key, pair.Value);
                foreach ((string handler, string[] inputs) in Gestures)
                {
                    if (!OverrideBodies(source, handler).Any(DoesSomething))
                        continue;

                    string help = WithBaseClasses(source, sources);
                    if (!inputs.Any(help.Contains))
                        missing.Add($"{file}: {handler} handles input but its help lists none of {string.Join(", ", inputs)}");
                }
            }

            Assert.AreEqual(0, missing.Count, string.Join("\n", missing));
        }

        [TestMethod]
        public void KeysAreHandledOnlyThroughKeyBindingTables()
        {
            // Modifier keys on their own (tracking Ctrl held) are state, not commands, and have no help line.
            Regex keyTest = new(@"(e|args)\.(KeyCode|KeyData|KeyChar)\s*(==|!=|is\b)(?!\s*Keys\.(Control|ControlKey|ShiftKey|Menu|LControlKey|RControlKey)\b)|switch\s*\(\s*(e|args)\.(KeyCode|KeyData|KeyChar)\s*\)");

            List<string> found = [];
            foreach (KeyValuePair<string, string> pair in Sources())
            {
                (string file, string source) = (pair.Key, pair.Value);
                // Comments are blanked but keep their line breaks, so the reported line numbers stay right.
                string code = Regex.Replace(source, @"//[^\n]*|/\*.*?\*/", c => new string('\n', c.Value.Count(ch => ch == '\n')), RegexOptions.Singleline);
                foreach (Match m in keyTest.Matches(code))
                {
                    int line = code.Take(m.Index).Count(c => c == '\n') + 1;
                    found.Add($"{file}:{line}: {m.Value.Trim()}");
                }
            }

            Assert.AreEqual(0, found.Count, "Key tests outside a KeyBinding table (help-entries.mdc):\n" + string.Join("\n", found));
        }
    }
}
