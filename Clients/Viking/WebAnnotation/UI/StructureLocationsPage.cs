using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.Common;
using WebAnnotation.ReviewFeed;
using WebAnnotation.StructureLocations;
using WebAnnotation.ViewModel;

namespace WebAnnotation.UI
{
    /// <summary>
    /// The Locations tab of a structure's properties: the structure's locations as a table. The rows come from the OData
    /// service without any shapes (<see cref="StructureLocationsODataQuery"/>), a page at a time, and each page is listed as
    /// it arrives, so a cell with thousands of locations shows its first rows quickly even on a slow connection. A line above
    /// the table says how far the load got, and a failed load says why and offers Retry instead of leaving the tab empty.
    /// </summary>
    [PropertyPage(typeof(Structure), 3)]
    public partial class StructureLocationsPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure? Obj;

        /// <summary>
        /// Counts the structures this page has been asked to show. A load carries the number it started with, so pages that
        /// arrive for a structure that is no longer shown are thrown away.
        /// </summary>
        private int shownObjectNumber;

        /// <summary>The <see cref="shownObjectNumber"/> whose list has been loaded or is loading; 0 when none has.</summary>
        private int loadedObjectNumber;

        /// <summary>Cancels the running load when another structure is shown or the page closes.</summary>
        private CancellationTokenSource? loadCancel;

        /// <summary>Progress, the final count, or why the load failed, with a Retry link after a failure.</summary>
        private readonly LinkLabel statusLabel;

        private const string RetryText = "Retry";

        public StructureLocationsPage()
        {
            InitializeComponent();
            Title = "Locations";
            listLocations.Title = "Locations";
            listLocations.TitleVisible = false;

            statusLabel = new LinkLabel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 20,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0),
            };
            statusLabel.LinkClicked += (_, _) => Retry();
            Controls.Add(statusLabel);
            // Docking is laid out from the last control added, so the label takes the top and the table fills the rest.
            statusLabel.SendToBack();
            listLocations.BringToFront();
        }

        protected override void OnInitPage() => base.OnInitPage();

        protected override void OnShowObject(object Object)
        {
            Obj = Object as Structure;
            Debug.Assert(Obj != null);

            // The list on screen, if any, belongs to the structure shown before. Clear it, and if the tab is already
            // showing, load the new structure's locations now: no VisibleChanged will come to do it.
            shownObjectNumber++;
            loadCancel?.Cancel();
            listLocations.ClearLocations();
            SetStatus("");
            if (Visible)
            {
                _ = LoadLocationsAsync();
            }
        }

        private void StructureLocationsPage_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                _ = LoadLocationsAsync();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            // The page is closing: stop asking the server for pages nobody will see.
            loadCancel?.Cancel();
            base.OnHandleDestroyed(e);
        }

        private void Retry()
        {
            loadedObjectNumber = 0;
            _ = LoadLocationsAsync();
        }

        private void SetStatus(string text, bool offerRetry = false)
        {
            statusLabel.Text = offerRetry ? text + " " + RetryText : text;
            statusLabel.LinkArea = offerRetry
                ? new LinkArea(statusLabel.Text.Length - RetryText.Length, RetryText.Length)
                : new LinkArea(0, 0);
        }

        /// <summary>
        /// Lists the structure's locations from OData, a page at a time: each page is downloaded and read off the UI thread and
        /// added to the table as soon as it arrives, then the next page is asked for until the server sends no next link. Does
        /// nothing when this structure's list is already loaded or loading.
        /// </summary>
        private async Task LoadLocationsAsync()
        {
            Structure? structure = Obj;
            if (structure is null || loadedObjectNumber == shownObjectNumber)
            {
                return;
            }

            int objectNumber = shownObjectNumber;
            loadedObjectNumber = objectNumber;
            long structureID = structure.ID;

            if (!ReviewChangeODataQuery.TryODataRoot(WebAnnotationModel.State.Endpoint?.AbsoluteUri ?? "", out Uri odataRoot))
            {
                SetStatus("This annotation server has no OData service to list locations from.");
                Trace.WriteLine($"No OData root for the Locations tab: annotation endpoint {WebAnnotationModel.State.Endpoint}");
                return;
            }

            loadCancel?.Cancel();
            loadCancel = new CancellationTokenSource();
            CancellationToken cancel = loadCancel.Token;

            // A load always starts from an empty table, so a retry after a partial load does not list rows twice.
            listLocations.ClearLocations();
            SetStatus("Loading locations...");
            Uri? page = StructureLocationsODataQuery.BuildUri(odataRoot, structureID);
            try
            {
                while (page is not null)
                {
                    Uri query = page;
                    (List<StructureLocationRow> rows, Uri? next) = await Task.Run(async () =>
                    {
                        string json = await StructureLocationsODataQuery.DownloadAsync(query, cancel).ConfigureAwait(false);
                        List<StructureLocationRow> parsed = StructureLocationsODataQuery.ParsePage(json, out Uri? nextLink);
                        return (parsed, nextLink);
                    }, cancel);

                    // Another structure was shown while the server answered, or the page is gone.
                    if (objectNumber != shownObjectNumber || IsDisposed || cancel.IsCancellationRequested)
                    {
                        return;
                    }

                    listLocations.AppendLocations([.. rows]);
                    page = next;
                    if (page is not null)
                    {
                        SetStatus($"Loading locations... {listLocations.LocationCount:N0} so far");
                    }
                }

                int count = listLocations.LocationCount;
                SetStatus(count == 1 ? "1 location" : $"{count:N0} locations");
            }
            catch (Exception e) when (cancel.IsCancellationRequested || objectNumber != shownObjectNumber || IsDisposed)
            {
                // Abandoned on purpose: another structure is shown or the page closed.
                Trace.WriteLine($"Listing the locations of structure {structureID} stopped: {e.Message}");
            }
            catch (Exception e)
            {
                // Keep the rows that arrived and say why the rest did not; Retry (or showing the tab again) starts over.
                Trace.WriteLine($"Could not list the locations of structure {structureID}: {e}");
                int count = listLocations.LocationCount;
                string reason = e is TaskCanceledException ? "the server did not answer in time" : e.Message;
                SetStatus(count == 0
                    ? $"Could not load the locations: {reason}."
                    : $"Loaded {count:N0} locations, then the rest failed: {reason}.", offerRetry: true);
                if (loadedObjectNumber == objectNumber)
                {
                    loadedObjectNumber = 0;
                }
            }
        }
    }
}
