using System;
using System.Diagnostics;
using System.Windows.Forms;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using UiState = Viking.UI.State;

namespace WebAnnotation
{
    /// <summary>
    /// Builds and opens SBFSEM-tools deep links via an Identity bounce (browser SSO).
    /// </summary>
    internal static class SbfsemToolsLauncher
    {
        /// <summary>
        /// Resolves the top-level cell for a structure by walking ParentID until null.
        /// </summary>
        public static long? GetRootStructureId(StructureObj? structure)
        {
            while (structure != null)
            {
                if (!structure.ParentID.HasValue)
                    return structure.ID;

                if (!Store.Structures.TryGetObjectByID(structure.ParentID.Value, out structure) || structure is null)
                    return null;
            }

            return null;
        }

        public static long? GetRootStructureIdForLocation(LocationObj? location)
        {
            if (location is null)
                return null;

            StructureObj? structure = location.Parent;
            if (structure is null && location.ParentID.HasValue)
            {
                if (!Store.Structures.TryGetObjectByID(location.ParentID.Value, out structure))
                    structure = null;
            }

            return GetRootStructureId(structure);
        }

        /// <summary>
        /// Identity volume name used in SBFSEM-tools links; falls back to VikingXML name.
        /// </summary>
        public static string? ResolveVolumeName()
        {
            if (!string.IsNullOrWhiteSpace(UiState.IdentityVolumeName))
                return UiState.IdentityVolumeName;

            return UiState.volume?.Name;
        }

        public static bool TryOpen(long cellId, long? locationId)
        {
            string? volumeName = ResolveVolumeName();
            if (string.IsNullOrWhiteSpace(volumeName))
                return false;

            // Identity-first bounce establishes/reuses the Identity cookie; then redirects to /open.
            string bounceBase = string.IsNullOrWhiteSpace(UiState.SbfsemToolsIdentityBounceUrl)
                ? "https://identity.codepharm.net:4001/SbfsemOpen/Redirect"
                : UiState.SbfsemToolsIdentityBounceUrl.Trim();

            var uriBuilder = new UriBuilder(bounceBase);
            var query = $"volume={Uri.EscapeDataString(volumeName)}&cells={cellId}";
            if (locationId.HasValue)
                query += $"&location={locationId.Value}";
            uriBuilder.Query = query;

            try
            {
                Process.Start(new ProcessStartInfo(uriBuilder.Uri.AbsoluteUri) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WebAnnotation] Failed to open SBFSEM-tools: {ex.Message}");
                MessageBox.Show(
                    $"Could not open SBFSEM-tools:\n{ex.Message}",
                    "Open in SBFSEM-tools",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        public static void AddOpenMenuItem(ContextMenuStrip menu, long cellId, long? locationId)
        {
            if (string.IsNullOrWhiteSpace(ResolveVolumeName()))
                return;

            ToolStripMenuItem item = new("Open in SBFSEM-tools");
            item.Click += (_, _) => TryOpen(cellId, locationId);
            menu.Items.Add(item);
        }
    }
}
