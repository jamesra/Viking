using System;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// Who may delete a structure from its context menu, and what <see cref="Structure.Delete"/> asks (decision
    /// D-StructureDeleteSave). The server deletes every location of a deleted structure with no undo, so the question names
    /// the structure and its location count. The structure's own user is the one in its Username column (shown as Last
    /// Editor; structures carry no separate creator); anyone else also needs Review access. The server remains the authority
    /// on rights: this only stops a request the server would be asked to carry out on someone else's work.
    /// </summary>
    internal static class StructureDeletePrompt
    {
        /// <summary>
        /// True when <paramref name="currentUsername"/> is the structure's user (case-insensitive, ignoring surrounding
        /// spaces) or <paramref name="hasReviewAccess"/> is set. A blank name on either side never counts as the same user.
        /// </summary>
        public static bool MayDelete(string? structureUsername, string? currentUsername, bool hasReviewAccess) =>
            hasReviewAccess || IsSameUser(structureUsername, currentUsername);

        /// <summary>How the structure is named in both messages, for example <c>Cell #180 "AII"</c>.</summary>
        public static string Name(string? typeName, long id, string? label)
        {
            string type = string.IsNullOrWhiteSpace(typeName) ? "Structure" : typeName!.Trim();
            string name = $"{type} #{id}";
            return string.IsNullOrWhiteSpace(label) ? name : $"{name} \"{label!.Trim()}\"";
        }

        /// <summary>The Yes/No question asked before the delete is sent; No is the safe answer.</summary>
        public static string Question(string structureName, long locationCount)
        {
            string locations = locationCount == 1 ? "1 location" : $"{locationCount} locations";
            return $"Delete {structureName} and its {locations}?\n\n" +
                   "This permanently erases the structure and every one of its locations from the annotation database. It cannot be undone.";
        }

        /// <summary>Shown instead of the question when <see cref="MayDelete"/> is false.</summary>
        public static string RefusalMessage(string structureName, string? structureUsername)
        {
            string whose = string.IsNullOrWhiteSpace(structureUsername)
                ? $"{structureName} has no recorded user."
                : $"{structureName} was last edited by {structureUsername!.Trim()}.";
            return whose + " Deleting a structure that is not yours needs Review access to this volume.";
        }

        private static bool IsSameUser(string? a, string? b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
            string.Equals(a!.Trim(), b!.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
