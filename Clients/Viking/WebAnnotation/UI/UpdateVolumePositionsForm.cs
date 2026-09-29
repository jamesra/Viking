using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Chooses the volume transform and section ranges for a volume-position correction.
    /// Cancel closes the dialog without starting a job. Start returns only after the range text parses and every number is in the volume.
    /// </summary>
    internal sealed class UpdateVolumePositionsForm : Form
    {
        private readonly ComboBox? _transformCombo;
        private readonly string? _fixedTransform;
        private readonly HashSet<int> _volumeSections;
        private readonly TextBox _sectionsText;
        private readonly TextBox _interpretation;
        private readonly Button _start;
        private SectionRangeParse _parse;
        private VolumeSectionSelection _selection;

        /// <summary>Transform group name, or null when the volume has no named transform and mosaic space is used.</summary>
        public string? TransformName { get; private set; }

        /// <summary>True when the section box was left blank.</summary>
        public bool IsAllSections { get; private set; }

        /// <summary>Parsed section numbers when <see cref="IsAllSections"/> is false.</summary>
        public IReadOnlyList<long> Sections { get; private set; } = [];

        /// <summary>
        /// Called from the Annotation menu on the UI thread.
        /// </summary>
        /// <param name="transformNames">Named transforms on the open volume. Empty means mosaic-only.</param>
        /// <param name="preferredTransform">Active or default transform to select when there is more than one.</param>
        /// <param name="volumeSectionNumbers">Section numbers that exist in the open volume.</param>
        public UpdateVolumePositionsForm(string[] transformNames, string? preferredTransform, IEnumerable<int> volumeSectionNumbers)
        {
            _volumeSections = volumeSectionNumbers as HashSet<int> ?? new HashSet<int>(volumeSectionNumbers);

            Text = "Update Volume Positions";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new System.Drawing.Size(540, 468);
            AutoScaleMode = AutoScaleMode.Font;

            var transformCaption = new Label
            {
                AutoSize = true,
                Location = new System.Drawing.Point(12, 12),
                Text = "Volume transform"
            };
            Controls.Add(transformCaption);

            if (transformNames.Length > 1)
            {
                _transformCombo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new System.Drawing.Point(12, 32),
                    Width = 516
                };
                _transformCombo.Items.AddRange(transformNames);
                int preferred = Array.IndexOf(transformNames, preferredTransform);
                _transformCombo.SelectedIndex = preferred >= 0 ? preferred : 0;
                Controls.Add(_transformCombo);
            }
            else
            {
                _fixedTransform = transformNames.Length == 1 ? transformNames[0] : null;
                string caption = _fixedTransform is null
                    ? "No volume transform. Mosaic positions are copied into volume space."
                    : "Transform: " + _fixedTransform;
                Controls.Add(new Label
                {
                    AutoSize = false,
                    Location = new System.Drawing.Point(12, 32),
                    Size = new System.Drawing.Size(516, 32),
                    Text = caption
                });
            }

            Controls.Add(new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(12, 72),
                Size = new System.Drawing.Size(516, 52),
                Text = "Sections (blank = all). Separate numbers or ranges with commas, spaces, or new lines. Example: 1-100, 250, 400-410. A range can cross missing sections; those numbers are skipped."
            });

            _sectionsText = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = false,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new System.Drawing.Point(12, 128),
                Size = new System.Drawing.Size(516, 148),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _sectionsText.TextChanged += (_, _) => RefreshInterpretation();
            Controls.Add(_sectionsText);

            Controls.Add(new Label
            {
                AutoSize = true,
                Location = new System.Drawing.Point(12, 284),
                Text = "Interpreted as:"
            });

            _interpretation = new TextBox
            {
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                TabStop = false,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new System.Drawing.Point(12, 304),
                Size = new System.Drawing.Size(516, 88),
                Text = "All sections"
            };
            Controls.Add(_interpretation);

            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(372, 404),
                Size = new System.Drawing.Size(75, 28)
            };
            _start = new Button
            {
                Text = "Start",
                Location = new System.Drawing.Point(453, 404),
                Size = new System.Drawing.Size(75, 28)
            };
            _start.Click += OnStart;
            Controls.Add(cancel);
            Controls.Add(_start);
            CancelButton = cancel;

            RefreshInterpretation();
        }

        private void OnStart(object? sender, EventArgs e)
        {
            RefreshInterpretation();
            if (!_start.Enabled)
                return;

            TransformName = _transformCombo is null
                ? _fixedTransform
                : _transformCombo.SelectedItem as string;
            IsAllSections = _parse.IsAllSections;
            Sections = _parse.IsAllSections ? _parse.Sections : _selection.Present;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void RefreshInterpretation()
        {
            _parse = SectionRangeParser.Parse(_sectionsText.Text);
            if (!_parse.Success)
            {
                _interpretation.Text = _parse.Interpretation;
                _start.Enabled = false;
                return;
            }

            if (_parse.IsAllSections)
            {
                if (_volumeSections.Count == 0)
                {
                    _interpretation.Text = "This volume has no sections.";
                    _start.Enabled = false;
                    return;
                }

                _interpretation.Text = _parse.Interpretation;
                _start.Enabled = true;
                return;
            }

            _selection = SectionRangeParser.SelectInVolume(_parse.Sections, _volumeSections);
            _interpretation.Text = _selection.Interpretation;
            _start.Enabled = _selection.CanStart;
        }
    }
}
