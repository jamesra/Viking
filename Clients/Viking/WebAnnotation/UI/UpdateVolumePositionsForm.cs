using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Chooses the sections for Update Volume Positions (<see cref="VolumePositionUpdateJob"/>), which always writes the stored
    /// volume positions under the volume's default transform; there is no transform to choose.
    /// Cancel closes the dialog without starting a job. Start is enabled when the range text parses and at least one requested section exists in the volume
    /// (or the box is blank and the volume has sections). Requested numbers missing from the volume are skipped and reported in the dialog; <see cref="Sections"/> holds only the ones that exist.
    /// </summary>
    internal sealed class UpdateVolumePositionsForm : Form
    {
        /// <summary>What the job does and when to run it, shown at the top of the dialog.</summary>
        internal const string HelpText =
            "Recomputes the stored volume positions of the annotations on the chosen sections from their section positions, " +
            "using the volume's default alignment. Run it after a section's alignment (.stos) changes, so searches, " +
            "exports and other programs that read the stored positions match the new alignment. Annotations are not moved.";

        private readonly HashSet<int> _volumeSections;
        private readonly TextBox _sectionsText;
        private readonly TextBox _interpretation;
        private readonly Button _start;
        private SectionRangeParse _parse;
        private VolumeSectionSelection _selection;

        /// <summary>True when the section box was left blank.</summary>
        public bool IsAllSections { get; private set; }

        /// <summary>Parsed section numbers when <see cref="IsAllSections"/> is false.</summary>
        public IReadOnlyList<long> Sections { get; private set; } = [];

        /// <summary>
        /// Called from the Annotation menu on the UI thread.
        /// </summary>
        /// <param name="defaultTransform">The volume's default transform, or null when the volume has none and mosaic space is used.</param>
        /// <param name="volumeSectionNumbers">Section numbers that exist in the open volume.</param>
        public UpdateVolumePositionsForm(string? defaultTransform, IEnumerable<int> volumeSectionNumbers)
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

            Controls.Add(new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(12, 8),
                Size = new System.Drawing.Size(516, 44),
                Text = HelpText
            });

            Controls.Add(new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(12, 54),
                Size = new System.Drawing.Size(516, 16),
                Text = defaultTransform is null
                    ? "No volume transform: section positions are stored as volume positions."
                    : "Default transform: " + defaultTransform
            });

            Controls.Add(new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(12, 76),
                Size = new System.Drawing.Size(516, 48),
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
