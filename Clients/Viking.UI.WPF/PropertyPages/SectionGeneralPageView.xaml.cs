using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using Viking.Common;
using Viking.UI.WPF.Models;
using Viking.VolumeModel;
using VolumeSection = Viking.VolumeModel.Section;

namespace Viking.UI.WPF.PropertyPages
{
    [PropertyPage("Viking.ViewModels.SectionViewModel, VikingCore", priority: 0)]
    public partial class SectionGeneralPageView : PropertyPageViewBase
    {
        private dynamic _section;
        private IDictionary _sectionMap;

        public SectionGeneralPageView()
        {
            InitializeComponent();
        }

        public override string Title => "General";

        protected override void OnContextUpdated(object context)
        {
            _section = context ?? throw new ArgumentNullException(nameof(context));
            dynamic volume = _section.VolumeViewModel;
            _sectionMap = volume?.SectionViewModels as IDictionary;

            SectionHeaderText.Text = $"{_section.Number} : {_section.Name}";
            LoadNotes(_section.Notes as string ?? string.Empty);
            PopulateReferenceLists();
        }

        private void LoadNotes(string notes)
        {
            TextRange range = new(NotesBox.Document.ContentStart, NotesBox.Document.ContentEnd)
            {
                Text = string.Empty
            };

            if (string.IsNullOrWhiteSpace(notes))
            {
                return;
            }

            try
            {
                using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(notes));
                range.Load(stream, DataFormats.Rtf);
            }
            catch
            {
                range.Text = notes;
            }
        }

        private void PopulateReferenceLists()
        {
            List<int> above = [];
            List<int> below = [];
            int currentNumber = (int)_section.Number;

            if (_sectionMap != null)
            {
                foreach (object key in _sectionMap.Keys)
                {
                    if (key is int number)
                    {
                        if (number > currentNumber)  // Higher numbers are above
                        {
                            above.Add(number);
                        }
                        else if (number < currentNumber)  // Lower numbers are below
                        {
                            below.Add(number);
                        }
                    }
                }
            }

            above.Sort();
            below.Sort();

            AboveList.ItemsSource = above;
            BelowList.ItemsSource = below;

            // Get the volume's local directory for settings
            string volumeLocalDir = GetVolumeLocalDirectory();

            // Load persisted settings
            var allSettings = SectionReferenceSettings.LoadForVolume(volumeLocalDir);

            int? aboveNumber = null;
            int? belowNumber = null;

            // Check if we have persisted settings for this section
            if (allSettings.TryGetValue(currentNumber, out var persistedRefs))
            {
                aboveNumber = persistedRefs.ReferenceAbove;
                belowNumber = persistedRefs.ReferenceBelow;
            }
            else
            {
                // No persisted settings, check if already set in the section model
                aboveNumber = GetSectionNumber(_section.ReferenceSectionAbove as VolumeSection);
                belowNumber = GetSectionNumber(_section.ReferenceSectionBelow as VolumeSection);

                // If not set in model, auto-select adjacent sections (defaults)
                if (!aboveNumber.HasValue)
                {
                    int defaultAbove = currentNumber + 1;
                    if (above.Contains(defaultAbove))
                    {
                        aboveNumber = defaultAbove;
                    }
                }

                if (!belowNumber.HasValue)
                {
                    int defaultBelow = currentNumber - 1;
                    if (below.Contains(defaultBelow))
                    {
                        belowNumber = defaultBelow;
                    }
                }
            }

            // Set selections and scroll into view
            if (aboveNumber.HasValue && above.Contains(aboveNumber.Value))
            {
                AboveList.SelectedItem = aboveNumber.Value;
                AboveList.ScrollIntoView(aboveNumber.Value);
            }

            if (belowNumber.HasValue && below.Contains(belowNumber.Value))
            {
                BelowList.SelectedItem = belowNumber.Value;
                BelowList.ScrollIntoView(belowNumber.Value);
            }
        }

        private static int? GetSectionNumber(VolumeSection section) => section?.Number;

        private string GetVolumeLocalDirectory()
        {
            try
            {
                dynamic volume = _section?.VolumeViewModel;
                if (volume is null)
                {
                    return null;
                }

                // Access the Volume object through the ViewModel
                dynamic volumeModel = volume.Volume;
                if (volumeModel is null)
                {
                    return null;
                }

                // Get LocalVolumeDir using the public property
                return volumeModel.LocalVolumeDir as string;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to get volume local directory: {ex.Message}");
                return null;
            }
        }

        private VolumeSection ResolveSection(int number)
        {
            if (_sectionMap is null)
            {
                return null;
            }

            if (_sectionMap.Contains(number) && _sectionMap[number] is object sectionViewModel)
            {
                FieldInfo field = sectionViewModel.GetType().GetField("section", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    if (field.GetValue(sectionViewModel) is VolumeSection typedSection)
                    {
                        return typedSection;
                    }
                }

                dynamic dynamicVm = sectionViewModel;
                return dynamicVm.section as VolumeSection;
            }

            return null;
        }

        public override void SaveChanges()
        {
            // Update the section model with selected values
            _section.ReferenceSectionAbove = AboveList.SelectedItem is int aboveNumber
                ? ResolveSection(aboveNumber)
                : null;

            _section.ReferenceSectionBelow = BelowList.SelectedItem is int belowNumber
                ? ResolveSection(belowNumber)
                : null;

            // Persist to volume-specific settings if different from defaults
            int currentNumber = (int)_section.Number;
            int? selectedAbove = AboveList.SelectedItem as int?;
            int? selectedBelow = BelowList.SelectedItem as int?;

            string volumeLocalDir = GetVolumeLocalDirectory();
            if (string.IsNullOrWhiteSpace(volumeLocalDir))
            {
                return;
            }

            var allSettings = SectionReferenceSettings.LoadForVolume(volumeLocalDir);

            // Calculate defaults
            var (defaultAbove, defaultBelow) = SectionReferenceSettings.GetDefaultReferences(currentNumber);

            // Check if current selections are different from defaults
            bool isAboveDefault = selectedAbove == defaultAbove;
            bool isBelowDefault = selectedBelow == defaultBelow;

            if (isAboveDefault && isBelowDefault)
            {
                // Selections match defaults, remove from settings
                allSettings.Remove(currentNumber);
            }
            else
            {
                // Selections differ from defaults, persist them
                allSettings[currentNumber] = new SectionReferences
                {
                    ReferenceAbove = selectedAbove,
                    ReferenceBelow = selectedBelow
                };
            }

            SectionReferenceSettings.SaveForVolume(volumeLocalDir, allSettings);
        }
    }
}

