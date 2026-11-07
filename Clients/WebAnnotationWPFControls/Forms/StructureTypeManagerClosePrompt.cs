using System;
using System.Diagnostics;

namespace WebAnnotation.WPF.Forms
{
    /// <summary>
    /// The question the F7 Structure Type Manager asks when it is closed with the title-bar X (or Alt+F4) while
    /// structure type or link rule edits were never applied. Yes saves them as Apply does; No closes and leaves them
    /// pending in the stores. Also turns a failed save into the message the window shows before staying open. Kept
    /// apart from the window so the wording, the "ask or not" rule and the failure rule are testable without a server.
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

        /// <summary>
        /// Runs the save Yes chose and returns null when everything reached the server, otherwise the error text the
        /// window shows before it stays open. The two stores report failure differently: the structure type store
        /// returns false and records the cause in its <c>LastSaveFault</c>, while the link store throws; both end up
        /// as one message here so neither failure closes the window silently.
        /// </summary>
        /// <param name="saveAllPending">The Apply save; false means the structure type save failed.</param>
        /// <param name="structureTypeSaveFault">Reads the structure type store's last save fault, which may be null.</param>
        public static string SaveFailureMessage(Func<bool> saveAllPending, Func<Exception> structureTypeSaveFault)
        {
            Exception failure;
            try
            {
                if (saveAllPending())
                    return null;

                failure = structureTypeSaveFault() ?? new InvalidOperationException("The structure type save did not complete.");
            }
            catch (Exception exp)
            {
                failure = exp;
            }

            Trace.WriteLine($"Structure Type Manager save on close failed: {failure}");
            return $"The changes could not be saved and are still pending:\n{failure.Message}";
        }
    }
}
