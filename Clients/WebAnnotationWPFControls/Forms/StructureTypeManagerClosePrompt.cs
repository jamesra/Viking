namespace WebAnnotation.WPF.Forms
{
    /// <summary>
    /// The question the F7 Structure Type Manager asks when it is closed with the title-bar X (or Alt+F4) while
    /// structure type or link rule edits were never applied. Yes saves them as Apply does; No closes and leaves them
    /// pending in the stores. Kept apart from the window so the wording and the "ask or not" rule are testable
    /// without a server.
    /// </summary>
    public static class StructureTypeManagerClosePrompt
    {
        /// <summary>
        /// Returns the Yes/No question naming the pending counts, or null when nothing is pending and the window
        /// closes without asking.
        /// </summary>
        /// <param name="pendingTypes">Structure types the next structure type save would send.</param>
        /// <param name="pendingLinkRules">Permitted link (link rule) changes the next link save would send.</param>
        public static string Question(int pendingTypes, int pendingLinkRules)
        {
            string types = pendingTypes > 0 ? Count(pendingTypes, "structure type") : null;
            string links = pendingLinkRules > 0 ? Count(pendingLinkRules, "link rule") : null;
            string pending = types is not null && links is not null ? $"{types} and {links}" : types ?? links;
            if (pending is null)
                return null;

            return $"You have unsaved changes to {pending}.\n\n" +
                   "Yes saves them now, as Apply does.\n" +
                   "No closes this window and keeps them pending; the next Apply saves them.";
        }

        private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
    }
}
