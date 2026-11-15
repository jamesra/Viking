using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebAnnotation.StructureLocations
{
    /// <summary>
    /// Fills a property tab's table from a paged OData list (<see cref="ODataPages"/>): each page is downloaded and read off the
    /// UI thread and added to the table as soon as it arrives, then the next page is asked for until the server sends no next
    /// link. A line above the table (<see cref="Status"/>) says how far the load got, the final count, or why it failed with a
    /// Retry link; rows that arrived before a failure stay listed. Used by the Locations and Child Structures tabs of a
    /// structure and the Structures tab of a structure type.
    /// </summary>
    internal sealed class ODataListLoad<TRow> where TRow : class
    {
        private const string RetryText = "Retry";

        private readonly Control host;
        private readonly string singular;
        private readonly string plural;
        private readonly Func<Uri, Uri> firstPage;
        private readonly Func<string, (List<TRow> Rows, Uri? Next)> parse;
        private readonly Action clear;
        private readonly Action<TRow[]> append;
        private readonly Func<int> count;

        /// <summary>
        /// Counts the objects the tab has been asked to show. A load carries the number it started with, so pages that arrive
        /// for an object that is no longer shown are thrown away.
        /// </summary>
        private int shownObjectNumber;

        /// <summary>The <see cref="shownObjectNumber"/> whose list has been loaded or is loading; 0 when none has.</summary>
        private int loadedObjectNumber;

        /// <summary>Cancels the running load when another object is shown or the tab closes.</summary>
        private CancellationTokenSource? loadCancel;

        /// <param name="host">The tab; the status line is docked to its top, above the table, which must fill the rest.</param>
        /// <param name="singular">What one row is, for the status line ("location").</param>
        /// <param name="plural">What the rows are ("locations").</param>
        /// <param name="firstPage">The first page's URL under the OData root, for the object shown now.</param>
        /// <param name="parse">Reads one response: its rows and the next page's link (null on the last).</param>
        /// <param name="clear">Empties the table.</param>
        /// <param name="append">Adds a page of rows to the table.</param>
        /// <param name="count">Rows in the table.</param>
        public ODataListLoad(Control host, string singular, string plural, Func<Uri, Uri> firstPage,
            Func<string, (List<TRow> Rows, Uri? Next)> parse, Action clear, Action<TRow[]> append, Func<int> count)
        {
            this.host = host;
            this.singular = singular;
            this.plural = plural;
            this.firstPage = firstPage;
            this.parse = parse;
            this.clear = clear;
            this.append = append;
            this.count = count;

            Status = new LinkLabel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 20,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0),
            };
            Status.LinkClicked += (_, _) => Retry();
            host.Controls.Add(Status);

            // Docking is laid out from the back of the z-order, so the line takes the top and the table fills what is left.
            Status.SendToBack();
            host.HandleDestroyed += (_, _) => loadCancel?.Cancel();
        }

        /// <summary>The progress line above the table.</summary>
        public LinkLabel Status { get; }

        /// <summary>
        /// Another object is shown: stop the running load, empty the table, and load again the next time
        /// <see cref="LoadIfNeededAsync"/> is called.
        /// </summary>
        public void Reset()
        {
            shownObjectNumber++;
            loadCancel?.Cancel();
            clear();
            SetStatus("");
        }

        private void Retry()
        {
            loadedObjectNumber = 0;
            _ = LoadIfNeededAsync();
        }

        private void SetStatus(string text, bool offerRetry = false)
        {
            Status.Text = offerRetry ? text + " " + RetryText : text;
            Status.LinkArea = offerRetry
                ? new LinkArea(Status.Text.Length - RetryText.Length, RetryText.Length)
                : new LinkArea(0, 0);
        }

        /// <summary>Loads the shown object's list unless it is already loaded or loading. Call it on the UI thread.</summary>
        public async Task LoadIfNeededAsync()
        {
            if (shownObjectNumber == 0 || loadedObjectNumber == shownObjectNumber)
            {
                return;
            }

            int objectNumber = shownObjectNumber;
            loadedObjectNumber = objectNumber;

            if (!ODataPages.TryRoot(out Uri odataRoot))
            {
                SetStatus($"This annotation server has no OData service to list {plural} from.");
                Trace.WriteLine($"No OData root to list {plural}: annotation endpoint {WebAnnotationModel.State.Endpoint}");
                return;
            }

            loadCancel?.Cancel();
            loadCancel = new CancellationTokenSource();
            CancellationToken cancel = loadCancel.Token;

            // A load always starts from an empty table, so a retry after a partial load does not list rows twice.
            clear();
            SetStatus($"Loading {plural}...");
            Uri? page = firstPage(odataRoot);
            try
            {
                while (page is not null)
                {
                    Uri query = page;
                    (List<TRow> rows, Uri? next) = await Task.Run(async () =>
                        parse(await ODataPages.DownloadAsync(query, cancel).ConfigureAwait(false)), cancel);

                    // Another object was shown while the server answered, or the tab is gone.
                    if (objectNumber != shownObjectNumber || host.IsDisposed || cancel.IsCancellationRequested)
                    {
                        return;
                    }

                    append([.. rows]);
                    page = next;
                    if (page is not null)
                    {
                        SetStatus($"Loading {plural}... {count():N0} so far");
                    }
                }

                int total = count();
                SetStatus(total == 1 ? $"1 {singular}" : $"{total:N0} {plural}");
            }
            catch (Exception e) when (cancel.IsCancellationRequested || objectNumber != shownObjectNumber || host.IsDisposed)
            {
                // Abandoned on purpose: another object is shown or the tab closed.
                Trace.WriteLine($"Listing {plural} stopped: {e.Message}");
            }
            catch (Exception e)
            {
                // Keep the rows that arrived and say why the rest did not; Retry (or showing the tab again) starts over.
                Trace.WriteLine($"Could not list {plural}: {e}");
                int loaded = count();
                string reason = ODataPages.Reason(e);
                SetStatus(loaded == 0
                    ? $"Could not load the {plural}: {reason}."
                    : $"Loaded {loaded:N0} {plural}, then the rest failed: {reason}.", offerRetry: true);
                if (loadedObjectNumber == objectNumber)
                {
                    loadedObjectNumber = 0;
                }
            }
        }
    }
}
