using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using Viking.Common;
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
            TextRange range = new TextRange(NotesBox.Document.ContentStart, NotesBox.Document.ContentEnd);
            range.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(notes))
            {
                return;
            }

            try
            {
                using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(notes));
                range.Load(stream, DataFormats.Rtf);
            }
            catch
            {
                range.Text = notes;
            }
        }

        private void PopulateReferenceLists()
        {
            List<int> above = new List<int>();
            List<int> below = new List<int>();

            if (_sectionMap != null)
            {
                int currentNumber = (int)_section.Number;
                foreach (object key in _sectionMap.Keys)
                {
                    if (key is int number)
                    {
                        if (number < currentNumber)
                        {
                            above.Add(number);
                        }
                        else if (number > currentNumber)
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

            int? aboveNumber = GetSectionNumber(_section.ReferenceSectionAbove as VolumeSection);
            int? belowNumber = GetSectionNumber(_section.ReferenceSectionBelow as VolumeSection);

            if (aboveNumber.HasValue)
            {
                AboveList.SelectedItem = aboveNumber.Value;
            }

            if (belowNumber.HasValue)
            {
                BelowList.SelectedItem = belowNumber.Value;
            }
        }

        private static int? GetSectionNumber(VolumeSection section)
        {
            return section?.Number;
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
            _section.ReferenceSectionAbove = AboveList.SelectedItem is int aboveNumber
                ? ResolveSection(aboveNumber)
                : null;

            _section.ReferenceSectionBelow = BelowList.SelectedItem is int belowNumber
                ? ResolveSection(belowNumber)
                : null;
        }
    }
}

