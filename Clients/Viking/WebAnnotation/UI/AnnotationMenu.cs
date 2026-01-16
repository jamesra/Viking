using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Viking.Common;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotation.View;
using WebAnnotation.ViewModel;

namespace WebAnnotation
{
    [MenuAttribute("Annotation")]
    internal class AnnotationMenu : Viking.Common.IMenuFactory
    {
        private static FindStructureNumberForm _FindStructureNumberForm = null;
        private static MergeStructuresForm _MergeStructuresForm = null;
        private static WebAnnotation.WPF.Forms.AnnotationPreferencesDialog _preferencesDialog = null;
        private static ToolStripMenuItem menuPenMode;
        private static CancellationTokenSource _opacityUpdateCancellationTokenSource;
        private static System.Threading.Timer _circleOpacityUpdateTimer;
        private static readonly object _circleOpacityUpdateLock = new object();
        private static double _pendingCircleOpacityParentless;
        private static double _pendingCircleOpacityWithParent;

        System.Windows.Forms.ToolStripItem Viking.Common.IMenuFactory.CreateMenuItem()
        {
            //Create a menu containing each of our bookmarks
            ToolStripMenuItem menuRoot = new ToolStripMenuItem("Annotation");

            ToolStripMenuItem menuFavoriteTypes = new ToolStripMenuItem("Choose Favorited Structure Types");
            menuFavoriteTypes.Click += OnChooseFavoriteStructureTypes;
            menuRoot.DropDownItems.Add(menuFavoriteTypes);

            ToolStripMenuItem menuPreferences = new ToolStripMenuItem("Preferences...");
            menuPreferences.Click += OnPreferences;
            menuRoot.DropDownItems.Add(menuPreferences);

            if (Global.Export != null)
            {
                //Create the option to hide bookmarks on the display
                ToolStripMenuItem menuExport = new ToolStripMenuItem("Export");

                //Create the option to hide bookmarks on the display
                ToolStripMenuItem menuExportMotifs = new ToolStripMenuItem("Motifs");

                ToolStripMenuItem menuExportMotifTLP = new ToolStripMenuItem("To Tulip Format");
                menuExportMotifTLP.Click += OnExportMotifsTLP;


                menuExportMotifs.DropDownItems.Add(menuExportMotifTLP);
                menuExport.DropDownItems.Add(menuExportMotifs);

                menuRoot.DropDownItems.Add(menuExport);
            }

            menuPenMode = new ToolStripMenuItem("Pen Mode")
            {
                Checked = WebAnnotation.Global.PenMode
            };
            menuPenMode.Click += OnPenMode;



            menuRoot.DropDownItems.Add(menuPenMode);


            return menuRoot;
        }

        public static void OnExportMotifsTLP(object sender, EventArgs e)
        {
            Debug.Print("OnExportMotifsTLP");

            Global.Export.OpenMotif();
        }

        public static void OnChooseFavoriteStructureTypes(object sender, EventArgs e)
        {
            Debug.Print("OnChooseFavoriteStructureTypes");
            UI.Forms.SelectStructureTypeForm StructureIDChoiceForm = new WebAnnotation.UI.Forms.SelectStructureTypeForm();
            Annotation.ViewModels.FavoriteStructureIDsViewModel favorite_view_model = new Annotation.ViewModels.FavoriteStructureIDsViewModel(Global.UserFavoriteStructureTypes);
            StructureIDChoiceForm.DataContext = favorite_view_model;
            StructureIDChoiceForm.Show();
        }

        public static void OnPreferences(object sender, EventArgs e)
        {
            Debug.Print("OnPreferences");
            
            // If dialog already exists and is open, just focus it
            if (_preferencesDialog != null && !_preferencesDialog.IsClosed)
            {
                _preferencesDialog.Focus();
                return;
            }

            // Create ViewModel and load current settings
            var viewModel = new WebAnnotation.WPF.Forms.AnnotationPreferencesDialogViewModel();
            viewModel.LoadCurrentSettings(
                Global.AnnotationSettings.NumSectionsInMemory,
                Global.AnnotationSettings.NumSectionsLoading,
                Global.AnnotationSettings.LocationTextScaleFactor,
                Global.AnnotationSettings.ReferenceLocationTextScaleFactor,
                Global.AnnotationSettings.DefaultClosedLineWidth,
                Global.AnnotationSettings.DefaultLocationJumpDownsample,
                Global.AnnotationSettings.AdjacentLocationRadiusScalar,
                Global.AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay,
                Global.AnnotationSettings.PenSimplifyThreshold,
                Global.AnnotationSettings.MinRadius,
                Global.AnnotationSettings.PolygonOpacityParentless,
                Global.AnnotationSettings.PolygonOpacityWithParent,
                Global.AnnotationSettings.CircleOpacityParentless,
                Global.AnnotationSettings.CircleOpacityWithParent,
                Global.AnnotationSettings.SegmentationPointRadius,
                Global.AnnotationSettings.PolygonPointRadius,
                Global.AnnotationSettings.SmallestRenderedSize
            );

            // Initialize static accessor properties
            CircleView.SmallestRenderedSizeAccessor = () => Global.AnnotationSettings.SmallestRenderedSize;
            LocationCanvasView.SmallestRenderedSizeAccessor = () => Global.AnnotationSettings.SmallestRenderedSize;

            // Wire up real-time preview for polygon opacity changes
            viewModel.PolygonOpacityChanged += (parentlessOpacity, withParentOpacity) =>
            {
                // Cancel previous task if running
                _opacityUpdateCancellationTokenSource?.Cancel();
                _opacityUpdateCancellationTokenSource?.Dispose();
                
                // Create new cancellation token source
                _opacityUpdateCancellationTokenSource = new CancellationTokenSource();
                
                // Start async update task
                _ = Task.Run(() => 
                {
                    try
                    {
                        UpdateVisibleSectionPolygonOpacity(
                            parentlessOpacity, 
                            withParentOpacity, 
                            _opacityUpdateCancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation is expected when user adjusts opacity again
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating polygon opacity: {ex.Message}");
                    }
                });
            };

            // Wire up real-time preview for circle opacity changes
            viewModel.CircleOpacityChanged += (parentlessOpacity, withParentOpacity) =>
            {
                // Use the same throttling mechanism as polygon opacity
                lock (_circleOpacityUpdateLock)
                {
                    _pendingCircleOpacityParentless = parentlessOpacity;
                    _pendingCircleOpacityWithParent = withParentOpacity;
                    
                    _circleOpacityUpdateTimer?.Dispose();
                    _circleOpacityUpdateTimer = new System.Threading.Timer(_ =>
                    {
                        lock (_circleOpacityUpdateLock)
                        {
                            _opacityUpdateCancellationTokenSource?.Cancel();
                            _opacityUpdateCancellationTokenSource?.Dispose();
                            _opacityUpdateCancellationTokenSource = new CancellationTokenSource();
                            
                            double parentless = _pendingCircleOpacityParentless;
                            double withParent = _pendingCircleOpacityWithParent;
                            
                            _ = Task.Run(() => 
                            {
                                try
                                {
                                    UpdateVisibleSectionCircleOpacity(
                                        parentless, 
                                        withParent, 
                                        _opacityUpdateCancellationTokenSource.Token);
                                }
                                catch (OperationCanceledException) { }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error updating circle opacity: {ex.Message}");
                                }
                            });
                        }
                    }, null, 100, Timeout.Infinite);
                }
            };

            _preferencesDialog = new WebAnnotation.WPF.Forms.AnnotationPreferencesDialog(viewModel);
            
            // Wire up event handlers to save settings
            _preferencesDialog.ApplyClicked += (s, args) => SaveSettingsFromViewModel(viewModel);
            _preferencesDialog.OkClicked += (s, args) => SaveSettingsFromViewModel(viewModel);
            
            _preferencesDialog.Show(); // Modeless dialog
        }

        private static void SaveSettingsFromViewModel(WebAnnotation.WPF.Forms.AnnotationPreferencesDialogViewModel viewModel)
        {
            Global.AnnotationSettings.NumSectionsInMemory = viewModel.NumSectionsInMemory;
            Global.AnnotationSettings.NumSectionsLoading = viewModel.NumSectionsLoading;
            Global.AnnotationSettings.LocationTextScaleFactor = viewModel.LocationTextScaleFactor;
            Global.AnnotationSettings.ReferenceLocationTextScaleFactor = viewModel.ReferenceLocationTextScaleFactor;
            Global.AnnotationSettings.DefaultClosedLineWidth = viewModel.DefaultClosedLineWidth;
            Global.AnnotationSettings.DefaultLocationJumpDownsample = viewModel.DefaultLocationJumpDownsample;
            Global.AnnotationSettings.AdjacentLocationRadiusScalar = viewModel.AdjacentLocationRadiusScalar;
            Global.AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay = viewModel.NumClosedCurveInterpolationPointsForDisplay;
            Global.AnnotationSettings.PenSimplifyThreshold = viewModel.PenSimplifyThreshold;
            Global.AnnotationSettings.MinRadius = viewModel.MinRadius;
            Global.AnnotationSettings.SegmentationPointRadius = viewModel.SegmentationPointRadius;
            Global.AnnotationSettings.PolygonPointRadius = viewModel.PolygonPointRadius;
            Global.AnnotationSettings.SmallestRenderedSize = viewModel.SmallestRenderedSize;
            
            // Update static accessor properties
            VikingXNAGraphics.CircleView.SmallestRenderedSizeAccessor = () => Global.AnnotationSettings.SmallestRenderedSize;
            WebAnnotation.View.LocationCanvasView.SmallestRenderedSizeAccessor = () => Global.AnnotationSettings.SmallestRenderedSize;
            
            float oldParentlessOpacity = Global.AnnotationSettings.PolygonOpacityParentless;
            float oldWithParentOpacity = Global.AnnotationSettings.PolygonOpacityWithParent;
            
            Global.AnnotationSettings.PolygonOpacityParentless = (float)viewModel.PolygonOpacityParentless;
            Global.AnnotationSettings.PolygonOpacityWithParent = (float)viewModel.PolygonOpacityWithParent;
            
            // Update all polygons in memory if opacity has changed
            if (oldParentlessOpacity != Global.AnnotationSettings.PolygonOpacityParentless ||
                oldWithParentOpacity != Global.AnnotationSettings.PolygonOpacityWithParent)
            {
                UpdateAllPolygonOpacityInMemory(
                    Global.AnnotationSettings.PolygonOpacityParentless,
                    Global.AnnotationSettings.PolygonOpacityWithParent);
            }
            
            float oldCircleOpacityParentless = Global.AnnotationSettings.CircleOpacityParentless;
            float oldCircleOpacityWithParent = Global.AnnotationSettings.CircleOpacityWithParent;
            
            Global.AnnotationSettings.CircleOpacityParentless = (float)viewModel.CircleOpacityParentless;
            Global.AnnotationSettings.CircleOpacityWithParent = (float)viewModel.CircleOpacityWithParent;
            
            // Update all circles in memory if opacity has changed
            if (oldCircleOpacityParentless != Global.AnnotationSettings.CircleOpacityParentless ||
                oldCircleOpacityWithParent != Global.AnnotationSettings.CircleOpacityWithParent)
            {
                UpdateAllCircleOpacityInMemory(
                    Global.AnnotationSettings.CircleOpacityParentless,
                    Global.AnnotationSettings.CircleOpacityWithParent);
            }
        }

        /// <summary>
        /// Applies opacity to a view based on whether it has a parent structure
        /// </summary>
        private static void ApplyOpacityToView(LocationCanvasView view, float parentlessOpacity, float withParentOpacity)
        {
            if (view is IColorView colorView)
            {
                bool hasParent = view.Parent?.ParentID.HasValue ?? false;
                float targetOpacity = hasParent ? withParentOpacity : parentlessOpacity;

                var currentColor = colorView.Color;
                colorView.Color = currentColor.SetAlpha(targetOpacity);
            }
        }

        /// <summary>
        /// Updates opacity for views of a specific type in the specified sections using parallel processing
        /// </summary>
        private static void UpdateOpacityForSections<T>(
            IEnumerable<int> sectionNumbers,
            float parentlessOpacity,
            float withParentOpacity,
            CancellationToken cancellationToken = default) where T : LocationCanvasView, IColorView
        {
            if (Viking.UI.State.volume?.SectionViewModels == null)
                return;

            // Collect sections that exist and have annotations loaded
            var sectionsToProcess = new List<SectionAnnotationsView>();
            
            foreach (var sectionNumber in sectionNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                SectionAnnotationsView sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(sectionNumber);
                if (sectionAnnotations != null)
                {
                    sectionsToProcess.Add(sectionAnnotations);
                }
            }

            if (sectionsToProcess.Count == 0)
                return;

            // Process all sections in parallel
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
            };

            Parallel.ForEach(sectionsToProcess, parallelOptions, sectionAnnotations =>
            {
                try
                {
                    // Collect all views of the specified type from this section
                    var views = sectionAnnotations.GetLocations()
                        .OfType<T>()
                        .ToList();

                    if (views.Count == 0)
                        return;

                    // Update all views in this section in parallel
                    Parallel.ForEach(views, parallelOptions, view =>
                    {
                        try
                        {
                            ApplyOpacityToView(view, parentlessOpacity, withParentOpacity);
                        }
                        catch
                        {
                            // Skip views that aren't ready for updates yet
                        }
                    });
                }
                catch
                {
                    // Skip sections that have issues
                }
            });
        }

        /// <summary>
        /// Updates opacity for polygons in the specified sections using parallel processing
        /// </summary>
        private static void UpdatePolygonOpacityForSections(
            IEnumerable<int> sectionNumbers,
            float parentlessOpacity,
            float withParentOpacity,
            CancellationToken cancellationToken = default)
        {
            UpdateOpacityForSections<LocationPolygonView>(
                sectionNumbers,
                parentlessOpacity,
                withParentOpacity,
                cancellationToken);
        }

        /// <summary>
        /// Updates opacity for all polygon views in memory using parallel processing
        /// </summary>
        private static void UpdateAllPolygonOpacityInMemory(
            float parentlessOpacity,
            float withParentOpacity)
        {
            if (Viking.UI.State.volume?.SectionViewModels == null)
                return;

            try
            {
                // Get all section numbers that might have annotations loaded
                var allSectionNumbers = Viking.UI.State.volume.SectionViewModels.Keys;
                
                // Update polygons in all sections
                UpdatePolygonOpacityForSections(
                    allSectionNumbers,
                    parentlessOpacity,
                    withParentOpacity);
                
                // Trigger redraw
                if (AnnotationOverlay.CurrentOverlay != null)
                {
                    AnnotationOverlay.CurrentOverlay.InvalidateParent();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating all polygon opacity in memory: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to collect visible section numbers (current and adjacent sections)
        /// </summary>
        private static List<int> GetVisibleSectionNumbers()
        {
            if (AnnotationOverlay.CurrentOverlay == null)
                return new List<int>();

            var overlay = AnnotationOverlay.CurrentOverlay;
            var currentSection = overlay.Parent.Section;
            
            // Collect visible section numbers: current first, then adjacent
            var visibleSectionNumbers = new List<int> { currentSection.Number };
            
            if (currentSection.ReferenceSectionAbove != null)
            {
                visibleSectionNumbers.Add(currentSection.ReferenceSectionAbove.Number);
            }
            
            if (currentSection.ReferenceSectionBelow != null)
            {
                visibleSectionNumbers.Add(currentSection.ReferenceSectionBelow.Number);
            }

            return visibleSectionNumbers;
        }

        /// <summary>
        /// Updates opacity for circles in the specified sections using parallel processing
        /// </summary>
        private static void UpdateCircleOpacityForSections(
            IEnumerable<int> sectionNumbers,
            float parentlessOpacity,
            float withParentOpacity,
            CancellationToken cancellationToken = default)
        {
            UpdateOpacityForSections<LocationCircleView>(
                sectionNumbers,
                parentlessOpacity,
                withParentOpacity,
                cancellationToken);
        }

        /// <summary>
        /// Updates opacity for all circle views in memory using parallel processing
        /// </summary>
        private static void UpdateAllCircleOpacityInMemory(
            float parentlessOpacity,
            float withParentOpacity)
        {
            if (Viking.UI.State.volume?.SectionViewModels == null)
                return;

            try
            {
                // Get all section numbers that might have annotations loaded
                var allSectionNumbers = Viking.UI.State.volume.SectionViewModels.Keys;
                
                // Update circles in all sections
                UpdateCircleOpacityForSections(
                    allSectionNumbers,
                    parentlessOpacity,
                    withParentOpacity);
                
                // Trigger redraw
                if (AnnotationOverlay.CurrentOverlay != null)
                {
                    AnnotationOverlay.CurrentOverlay.InvalidateParent();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating all circle opacity in memory: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates opacity for visible section circle views in real-time for preview using parallel processing
        /// </summary>
        private static void UpdateVisibleSectionCircleOpacity(
            double parentlessOpacity, 
            double withParentOpacity, 
            CancellationToken cancellationToken)
        {
            if (AnnotationOverlay.CurrentOverlay == null)
                return;

            var overlay = AnnotationOverlay.CurrentOverlay;
            var visibleSectionNumbers = GetVisibleSectionNumbers();
            
            if (visibleSectionNumbers.Count == 0)
                return;
            
            // Update circles in visible sections
            UpdateCircleOpacityForSections(
                visibleSectionNumbers,
                (float)parentlessOpacity,
                (float)withParentOpacity,
                cancellationToken);
            
            // Trigger redraw on UI thread
            overlay.InvalidateParent();
        }

        /// <summary>
        /// Updates opacity for visible section polygon views in real-time for preview using parallel processing
        /// </summary>
        private static void UpdateVisibleSectionPolygonOpacity(
            double parentlessOpacity, 
            double withParentOpacity, 
            CancellationToken cancellationToken)
        {
            if (AnnotationOverlay.CurrentOverlay == null)
                return;

            var overlay = AnnotationOverlay.CurrentOverlay;
            var visibleSectionNumbers = GetVisibleSectionNumbers();
            
            if (visibleSectionNumbers.Count == 0)
                return;
            
            // Update polygons in visible sections
            UpdatePolygonOpacityForSections(
                visibleSectionNumbers,
                (float)parentlessOpacity,
                (float)withParentOpacity,
                cancellationToken);
            
            // Trigger redraw on UI thread
            overlay.InvalidateParent();
        }

        [MenuItem("Show Pen Input Window")]
        public static void OnShowPenInputWindow(object sender, EventArgs e)
        {
            Debug.Print("OnShowPenInputWindow");

            if (Global.PenAnnotationForm == null || Global.PenAnnotationForm.IsDisposed)
            {
                Global.PenAnnotationForm = new UI.Forms.PenAnnotationViewForm(Viking.UI.State.ViewerForm.Section);
                Global.PenAnnotationForm.Show();
            }
            else
            {
                Global.PenAnnotationForm.Visible = !Global.PenAnnotationForm.Visible;
            }
        }

        [MenuItem("Open Last Modified Location")]
        public static void GoToLastModifiedLocation(object sender, EventArgs e)
        {
            AnnotationOverlay.GotoLastModifiedLocation();
        }

        public static void OnPenMode(object sender, EventArgs e)
        {
            Global.PenMode = !Global.PenMode;
            menuPenMode.Checked = Global.PenMode;
        }

        [MenuItem("Open Structure")]
        public static void ShowStructure(object sender, EventArgs e)
        {
            Debug.Print("Show Structure");

            if (_FindStructureNumberForm == null)
            {
                _FindStructureNumberForm = new FindStructureNumberForm();
            }
            else if (_FindStructureNumberForm.IsDisposed)
            {
                _FindStructureNumberForm = new FindStructureNumberForm();
            }

            _FindStructureNumberForm.Show();
            _FindStructureNumberForm.Focus();
        }

        [MenuItem("Goto Structure")]
        public static void GotoStructure(object sender, EventArgs e)
        {
            Debug.Print("Goto Structure");

            WebAnnotation.AnnotationOverlay.CurrentOverlay.OpenGotoStructureForm();
        }

        [MenuItem("Goto Location")]
        public static void GotoLocation(object sender, EventArgs e)
        {
            Debug.Print("Goto Location");

            WebAnnotation.AnnotationOverlay.CurrentOverlay.OpenGotoLocationForm();
        }

        [MenuItem("Merge Structures")]
        public static void MergeStructures(object sender, EventArgs e)
        {
            Debug.Print("Merge Structures");

            if (_MergeStructuresForm == null)
            {
                _MergeStructuresForm = new MergeStructuresForm();
            }
            else if (_MergeStructuresForm.IsDisposed)
            {
                _MergeStructuresForm = new MergeStructuresForm();
            }

            _MergeStructuresForm.ShowDialog();
            _MergeStructuresForm.Focus();
        }

        [MenuItem("Export")]
        public static void Export(object sender, EventArgs e)
        {
            Debug.Print("Export");


        }


    }
}
