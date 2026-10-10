using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using WebAnnotation;
using WebAnnotation.UI;
using WebAnnotationModel;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// WPF list of Review change cards. Hosted in <see cref="ReviewChangeFeedTab"/> via ElementHost.
    /// Double-click or Enter flies via the existing Goto Location path.
    /// Card rebuild and mosaic thumbs run off the UI thread; thumbnails reuse across reloads when
    /// the location id and LastModified are unchanged.
    /// </summary>
    public partial class ReviewChangeFeedView : UserControl
    {
        const int MaxConcurrentThumbLoads = 3;
        static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Square thumbnail edge length in DIPs. Updated when the ACTION pane / list width changes
        /// so cards track the divider between the tab control and the image view.
        /// </summary>
        public static readonly DependencyProperty CardThumbSizeProperty =
            DependencyProperty.Register(
                nameof(CardThumbSize),
                typeof(double),
                typeof(ReviewChangeFeedView),
                new PropertyMetadata(240.0));

        readonly ObservableCollection<ReviewChangeCardViewModel> _cards = new();
        readonly SemaphoreSlim _thumbGate = new(MaxConcurrentThumbLoads);
        ReviewChangeFeed _feed;
        ReviewChangeFeedFilter _filter;
        CancellationTokenSource _reloadCts;
        int _reloadGeneration;
        int _debounceGeneration;

        /// <summary>
        /// Bound by each card <c>Image</c>. Equals the list content width minus chrome/scrollbar.
        /// </summary>
        public double CardThumbSize
        {
            get => (double)GetValue(CardThumbSizeProperty);
            set => SetValue(CardThumbSizeProperty, value);
        }

        public ReviewChangeFeedView()
        {
            InitializeComponent();
            FeedList.ItemsSource = _cards;
            AttachFilter(ReviewChangeFeedFilter.Session);
            Loaded += (_, _) => UpdateCardThumbSize();
            SizeChanged += (_, _) => UpdateCardThumbSize();
            Unloaded += OnUnloaded;
        }

        void FeedList_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateCardThumbSize();

        void UpdateCardThumbSize()
        {
            if (FeedList is null)
                return;

            double listWidth = FeedList.ActualWidth;
            if (listWidth <= 0)
                listWidth = ActualWidth;

            double thumb = ReviewChangeCardLayout.ComputeCardThumbSize(
                listWidth,
                SystemParameters.VerticalScrollBarWidth);
            if (Math.Abs(CardThumbSize - thumb) > 0.5)
                CardThumbSize = thumb;
        }

        void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _reloadCts?.Cancel();
            _reloadCts?.Dispose();
            _reloadCts = null;
        }

        /// <summary>
        /// Binds to a feed (usually <see cref="ReviewChangeFeed.Session"/>). Safe to call once after construction.
        /// </summary>
        public void Attach(ReviewChangeFeed feed)
        {
            if (_feed != null)
                _feed.Changed -= OnFeedChanged;

            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _feed.Changed += OnFeedChanged;
            UpdateSeedStatusBanner();
            ScheduleReloadCards();
        }

        void AttachFilter(ReviewChangeFeedFilter filter)
        {
            if (_filter != null)
                _filter.Changed -= OnFilterChanged;

            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _filter.Changed += OnFilterChanged;
            LoadFilterControls();
        }

        void OnFeedChanged(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateSeedStatusBanner();
                    ScheduleReloadCards();
                }));
                return;
            }

            UpdateSeedStatusBanner();
            ScheduleReloadCards();
        }

        /// <summary>
        /// Shows or clears the OData seed failure line under the tab header.
        /// </summary>
        void UpdateSeedStatusBanner()
        {
            if (SeedStatusText is null)
                return;

            string message = _feed?.SeedStatusMessage ?? "";
            if (string.IsNullOrWhiteSpace(message))
            {
                SeedStatusText.Text = "";
                SeedStatusText.Visibility = Visibility.Collapsed;
                return;
            }

            SeedStatusText.Text = message;
            SeedStatusText.Visibility = Visibility.Visible;
        }

        void OnFilterChanged(object sender, EventArgs e)
        {
            // Immediate reload applies client-side gates (hide deleted / hide own) to the current
            // feed; OData re-seed then replaces rows. Mask-only thumbs are not reused (HasEmTexture).
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadFilterControls();
                    ScheduleReloadCards();
                    ReviewChangeFeedBackfill.StartAsync(_feed, _filter);
                }));
                return;
            }

            LoadFilterControls();
            ScheduleReloadCards();
            ReviewChangeFeedBackfill.StartAsync(_feed, _filter);
        }

        /// <summary>
        /// Debounces rapid <see cref="ReviewChangeFeed.Changed"/> bursts (poll + seed), then filters
        /// and builds card view-models on a worker. Only collection updates hit the UI thread.
        /// </summary>
        void ScheduleReloadCards()
        {
            if (_feed is null)
                return;

            int debounce = Interlocked.Increment(ref _debounceGeneration);
            _ = DebouncedReloadAsync(debounce);
        }

        async Task DebouncedReloadAsync(int debounce)
        {
            try
            {
                await Task.Delay(ReloadDebounce).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (debounce != _debounceGeneration)
                return;

            await Dispatcher.InvokeAsync(StartReloadCards);
        }

        void StartReloadCards()
        {
            if (_feed is null)
                return;

            _reloadCts?.Cancel();
            _reloadCts?.Dispose();
            _reloadCts = new CancellationTokenSource();
            CancellationToken token = _reloadCts.Token;
            int generation = Interlocked.Increment(ref _reloadGeneration);

            ReviewChangeFeed feed = _feed;
            ReviewChangeFeedFilter filter = _filter ?? ReviewChangeFeedFilter.Session;
            string currentUser = Viking.UI.State.UserCredentials?.UserName;

            // Reuse only thumbs that include an EM crop — mask-only results must be retried.
            var priorThumbs = new Dictionary<long, (DateTime ModifiedUtc, BitmapSource Thumb)>();
            foreach (ReviewChangeCardViewModel card in _cards)
            {
                if (card?.Thumbnail is null || !card.HasEmTexture)
                    continue;
                priorThumbs[card.LocationId] = (card.LastModifiedUtc, card.Thumbnail);
            }

            _ = Task.Run(() =>
            {
                try
                {
                    IReadOnlyList<ReviewChangeEntry> entries = feed.Entries;
                    var built = new List<(ReviewChangeCardViewModel Card, ReviewChangeEntry Entry, bool NeedThumb)>();
                    foreach (ReviewChangeEntry entry in entries)
                    {
                        if (token.IsCancellationRequested)
                            return;
                        if (!filter.Allows(entry, currentUser))
                            continue;

                        ReviewChangeCardViewModel card = ReviewChangeCardViewModel.FromEntry(entry);
                        bool needThumb = !entry.IsDeleted;
                        if (needThumb
                            && priorThumbs.TryGetValue(entry.LocationId, out var prior)
                            && prior.ModifiedUtc == entry.LastModifiedUtc
                            && prior.Thumb != null)
                        {
                            card.SetThumbnail(prior.Thumb, hasEmTexture: true);
                            needThumb = false;
                        }

                        built.Add((card, entry, needThumb));
                    }

                    if (token.IsCancellationRequested)
                        return;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (token.IsCancellationRequested || generation != _reloadGeneration)
                            return;

                        _cards.Clear();
                        foreach ((ReviewChangeCardViewModel card, ReviewChangeEntry entry, bool needThumb) in built)
                        {
                            _cards.Add(card);
                            if (needThumb)
                                _ = LoadThumbnailAsync(card, entry, token);
                        }
                    }));
                }
                catch (OperationCanceledException)
                {
                    // Superseded reload.
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"Review feed reload: {ex.Message}",
                        "WebAnnotation");
                }
            }, token);
        }

        async Task LoadThumbnailAsync(
            ReviewChangeCardViewModel card,
            ReviewChangeEntry entry,
            CancellationToken token)
        {
            try
            {
                await _thumbGate.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                Bitmap em = await ReviewChangeMosaicCropLoader
                    .TryLoadEmCropAsync(entry, ReviewChangeMaskComposer.DefaultSize, token)
                    .ConfigureAwait(false);

                if (token.IsCancellationRequested)
                {
                    em?.Dispose();
                    return;
                }

                bool hasEm = em != null;
                DrawingColor? tint = null;
                if (entry.StructureId.HasValue)
                    tint = DrawingColor.FromArgb(unchecked((int)StructureObj.ColorForId(entry.StructureId.Value)));

                BitmapSource thumb;
                using (em)
                using (Bitmap composed = ReviewChangeMaskComposer.Compose(
                    entry,
                    ReviewChangeMaskComposer.DefaultSize,
                    em,
                    tint))
                {
                    thumb = ReviewChangeCardViewModel.ToBitmapSource(composed);
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                        card.SetThumbnail(thumb, hasEmTexture: hasEm);
                });
            }
            catch (OperationCanceledException)
            {
                // Feed refreshed.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Review feed thumb {entry.LocationId}: {ex.Message}",
                    "WebAnnotation");
            }
            finally
            {
                _thumbGate.Release();
            }
        }

        void LoadFilterControls()
        {
            if (_filter is null || HideOwnCheck is null)
                return;

            HideOwnCheck.IsChecked = _filter.HideOwnChanges;
            if (HideDeletedCheck != null)
                HideDeletedCheck.IsChecked = _filter.HideDeleted;
            WatchedUsersBox.Text = _filter.WatchedUsersText ?? "";
            StructureOrLabelBox.Text = _filter.StructureOrLabelText ?? "";
            if (SectionsBox != null && SectionsBox.Text != (_filter.SectionsText ?? ""))
                SectionsBox.Text = _filter.SectionsText ?? "";
            UpdateSectionsInterpretation();
        }

        void SectionsBox_OnTextChanged(object sender, TextChangedEventArgs e) =>
            UpdateSectionsInterpretation();

        void UpdateSectionsInterpretation()
        {
            if (SectionsInterpretation is null || SectionsBox is null)
                return;

            SectionRangeParse parse = SectionRangeParser.Parse(SectionsBox.Text);
            SectionsInterpretation.Text = parse.Interpretation;
            SectionsInterpretation.Foreground = parse.Success
                ? System.Windows.Media.Brushes.Gray
                : System.Windows.Media.Brushes.OrangeRed;
        }

        void FilterToggleButton_OnClick(object sender, RoutedEventArgs e)
        {
            FilterPanel.Visibility = FilterPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        void FilterApplyButton_OnClick(object sender, RoutedEventArgs e)
        {
            SectionRangeParse parse = SectionRangeParser.Parse(SectionsBox?.Text);
            if (!parse.Success)
            {
                UpdateSectionsInterpretation();
                return;
            }

            _filter?.Apply(
                HideOwnCheck.IsChecked == true,
                WatchedUsersBox.Text,
                StructureOrLabelBox.Text,
                SectionsBox?.Text ?? "",
                hideDeleted: HideDeletedCheck?.IsChecked == true);
        }

        void FilterClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            HideOwnCheck.IsChecked = false;
            if (HideDeletedCheck != null)
                HideDeletedCheck.IsChecked = true;
            WatchedUsersBox.Text = "";
            StructureOrLabelBox.Text = "";
            if (SectionsBox != null)
                SectionsBox.Text = "";
            _filter?.Apply(
                hideOwnChanges: false,
                watchedUsersText: "",
                structureOrLabelText: "",
                sectionsText: "",
                hideDeleted: true);
        }

        void FeedList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => ActivateSelected();

        void FeedList_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ActivateSelected();
                e.Handled = true;
            }
        }

        void ActivateSelected()
        {
            if (FeedList.SelectedItem is not ReviewChangeCardViewModel card)
                return;
            if (card.IsDeleted)
                return;

            AnnotationOverlay.GoToLocation(card.LocationId);
        }
    }

    /// <summary>
    /// One bound row: caption, time, username, and a composed thumbnail bitmap.
    /// </summary>
    public sealed class ReviewChangeCardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public long LocationId { get; private set; }
        public DateTime LastModifiedUtc { get; private set; }
        public string Caption { get; private set; }
        public string TimeLabel { get; private set; }
        public string UsernameLabel { get; private set; }
        public string DeletedRowText { get; private set; }
        public bool IsDeleted { get; private set; }
        public BitmapSource Thumbnail { get; private set; }

        /// <summary>
        /// True when <see cref="Thumbnail"/> includes an EM crop (not mask-on-empty only).
        /// Mask-only thumbs are shown but not reused across reloads so EM can be retried.
        /// </summary>
        public bool HasEmTexture { get; private set; }

        /// <summary>
        /// Builds a card with no bitmap work. Thumbnails are filled by <see cref="ReviewChangeFeedView"/>
        /// on a background task so the UI thread stays free at startup.
        /// </summary>
        public static ReviewChangeCardViewModel FromEntry(ReviewChangeEntry entry)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            return new ReviewChangeCardViewModel
            {
                LocationId = entry.LocationId,
                LastModifiedUtc = entry.LastModifiedUtc,
                Caption = entry.Caption,
                TimeLabel = entry.TimeLabel,
                UsernameLabel = entry.UsernameLabel,
                DeletedRowText = entry.DeletedRowText,
                IsDeleted = entry.IsDeleted,
                Thumbnail = null,
                HasEmTexture = false
            };
        }

        /// <summary>Replaces the placeholder once the server mosaic crop is composed.</summary>
        public void SetThumbnail(BitmapSource thumbnail, bool hasEmTexture = true)
        {
            Thumbnail = thumbnail;
            HasEmTexture = hasEmTexture && thumbnail != null;
            OnPropertyChanged(nameof(Thumbnail));
        }

        /// <summary>
        /// Copies GDI pixels into a frozen WPF <see cref="WriteableBitmap"/> (no PNG encode).
        /// Faster than round-tripping through <see cref="ImageFormat.Png"/> for 50 card thumbs.
        /// </summary>
        public static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            if (bitmap is null)
                throw new ArgumentNullException(nameof(bitmap));

            int width = bitmap.Width;
            int height = bitmap.Height;
            var rect = new System.Drawing.Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                int bufferSize = stride * height;
                var pixels = new byte[bufferSize];
                Marshal.Copy(data.Scan0, pixels, 0, bufferSize);

                var image = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                image.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
                image.Freeze();
                return image;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
