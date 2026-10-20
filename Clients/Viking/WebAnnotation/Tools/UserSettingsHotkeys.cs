using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;
using Viking.AnnotationServiceTypes.Interfaces;
using Keys = System.Windows.Forms.Keys;

namespace WebAnnotation.Tools
{
    public enum HotkeyCommandKind
    {
        CreateStructure,
        ToggleStructureTag,
        ToggleLocationTag,
        ToggleLocationTerminal,
        ChangeLocationType,
    }

    /// <summary>One shortcut from the volume's <c>UserSettings.xml</c>, resolved to the command it runs.</summary>
    public sealed record HotkeyCommand(
        HotkeyCommandKind Kind,
        string Name,
        long TypeId = 0,
        LocationType LocationType = LocationType.CIRCLE,
        IReadOnlyList<string> Tags = null,
        string Tag = null,
        string Value = null);

    /// <summary>
    /// The shortcuts Viking reads from the volume's <c>DefaultWebAnnotationUserSettings</c> file, for clients that do not
    /// run Viking's overlay (Jotunn). Commands that open Viking forms (<c>Action</c> entries) are not supported.
    /// </summary>
    public sealed class UserSettingsHotkeys
    {
        const string FileName = "UserSettings.xml";

        /// <summary>How long a downloaded copy is used before it is fetched again.</summary>
        static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(1);

        readonly UserSettings _settings;

        UserSettingsHotkeys(UserSettings settings)
        {
            _settings = settings;
        }

        /// <summary>Reads a settings file; throws when it is not a readable settings document.</summary>
        public static UserSettingsHotkeys Load(string path) => new(UserSettings.Load(path));

        /// <summary>
        /// The volume's shortcuts: the copy cached in <paramref name="cacheDirectory"/>, downloaded again from the
        /// <c>DefaultWebAnnotationUserSettings</c> uri in <paramref name="volumeElement"/> when missing, stale or unreadable.
        /// Null when no copy can be read.
        /// </summary>
        public static async Task<UserSettingsHotkeys> LoadAsync(XElement volumeElement, string cacheDirectory, CancellationToken cancellationToken = default)
        {
            string path = Path.Combine(cacheDirectory, FileName);
            Uri uri = SettingsUri(volumeElement);

            bool stale = !File.Exists(path) || DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > CacheLifetime;
            if (stale && uri != null)
                await TryDownloadAsync(uri, path, cancellationToken).ConfigureAwait(false);

            UserSettingsHotkeys loaded = TryLoad(path);
            if (loaded == null && !stale && uri != null && await TryDownloadAsync(uri, path, cancellationToken).ConfigureAwait(false))
                loaded = TryLoad(path);
            return loaded;
        }

        /// <summary>The <c>Uri</c> attribute of the volume's <c>DefaultWebAnnotationUserSettings</c> element, or null.</summary>
        public static Uri SettingsUri(XElement volumeElement)
        {
            string value = volumeElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "DefaultWebAnnotationUserSettings")?.Attribute("Uri")?.Value;
            return Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ? uri : null;
        }

        /// <summary>
        /// The command bound to <paramref name="key"/> with exactly these modifiers, or null. Keys Viking's overlay handles
        /// itself (<see cref="OverlayHotkeys"/>) never match, as in Viking.
        /// </summary>
        public HotkeyCommand Find(Keys key, bool shift, bool ctrl, bool alt)
        {
            Keys keyData = key | (shift ? Keys.Shift : Keys.None) | (ctrl ? Keys.Control : Keys.None) | (alt ? Keys.Alt : Keys.None);
            if (key == Keys.None || OverlayHotkeys.IsReserved(keyData))
                return null;

            foreach (Hotkey hotkey in _settings.Shortcuts.Hotkey)
            {
                if (hotkey.KeyCode != key || hotkey.Shift != shift || hotkey.Ctrl != ctrl || hotkey.Alt != alt)
                    continue;

                HotkeyCommand command = Resolve(hotkey.Action);
                if (command != null)
                    return command;
            }

            return null;
        }

        /// <summary>WPF key overload: converts through the virtual-key code, which both toolkits share.</summary>
        public HotkeyCommand Find(System.Windows.Input.Key key, bool shift, bool ctrl, bool alt) =>
            Find((Keys)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key), shift, ctrl, alt);

        /// <summary>"Ctrl+C: Annotate Cell with Circle" lines for a help panel, in file order.</summary>
        public IReadOnlyList<string> HelpLines =>
            _settings.Shortcuts.Hotkey
                .Where(h => Resolve(h.Action) != null)
                .Select(h => $"{h.BuildModifierString()}{h.KeyName}: {h.Action}")
                .ToList();

        HotkeyCommand Resolve(string name)
        {
            UserSettings.ActionsLocalType actions = _settings.Actions;

            CreateStructureCommandAction create = actions.CreateStructureCommandAction.FirstOrDefault(a => a.Name == name);
            if (create != null)
                return new HotkeyCommand(HotkeyCommandKind.CreateStructure, name, TypeId: (long)create.TypeID,
                    LocationType: create.GetLocationType(), Tags: create.AttributeList.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray());

            ToggleStructureTagCommandAction structureTag = actions.ToggleStructureTagCommandAction.FirstOrDefault(a => a.Name == name);
            if (structureTag != null)
                return new HotkeyCommand(HotkeyCommandKind.ToggleStructureTag, name, Tag: structureTag.Tag, Value: structureTag.Value);

            ToggleLocationTagCommandAction locationTag = actions.ToggleLocationTagCommandAction.FirstOrDefault(a => a.Name == name);
            if (locationTag != null)
                return new HotkeyCommand(HotkeyCommandKind.ToggleLocationTag, name, Tag: locationTag.Tag, Value: locationTag.Value);

            if (actions.ToggleLocationTerminalCommandAction.Any(a => a.Name == name))
                return new HotkeyCommand(HotkeyCommandKind.ToggleLocationTerminal, name);

            ChangeLocationAnnotationTypeAction change = actions.ChangeLocationAnnotationTypeAction.FirstOrDefault(a => a.Name == name);
            if (change != null)
                return new HotkeyCommand(HotkeyCommandKind.ChangeLocationType, name, LocationType: change.GetLocationType());

            return null;
        }

        static UserSettingsHotkeys TryLoad(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                return Load(path);
            }
            catch (Exception ex) when (ex is System.Xml.XmlException || ex is Xml.Schema.Linq.LinqToXsdException || ex is IOException)
            {
                Trace.WriteLine($"Could not read user settings {path}: {ex.Message}");
                return null;
            }
        }

        static async Task<bool> TryDownloadAsync(Uri uri, string path, CancellationToken cancellationToken)
        {
            try
            {
                using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };
                byte[] data = await client.GetByteArrayAsync(uri).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".download";
                File.WriteAllBytes(temp, data);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // A failed download keeps the copy already on disk, as Viking does.
                Trace.WriteLine($"Could not download user settings {uri}: {ex.Message}");
                return false;
            }
        }
    }
}
