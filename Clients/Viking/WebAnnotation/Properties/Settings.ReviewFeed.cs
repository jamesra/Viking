namespace WebAnnotation.Properties
{
    /// <summary>
    /// Persisted Review Changes tab filter preferences (partial of <see cref="Settings"/>).
    /// </summary>
    internal sealed partial class Settings
    {
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ReviewFeedHideOwnChanges
        {
            get => (bool)this[nameof(ReviewFeedHideOwnChanges)];
            set => this[nameof(ReviewFeedHideOwnChanges)] = value;
        }

        /// <summary>When true, the Review Changes tab omits deleted location rows. Defaults to hidden.</summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ReviewFeedHideDeleted
        {
            get => (bool)this[nameof(ReviewFeedHideDeleted)];
            set => this[nameof(ReviewFeedHideDeleted)] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ReviewFeedWatchedUsers
        {
            get => (string)this[nameof(ReviewFeedWatchedUsers)];
            set => this[nameof(ReviewFeedWatchedUsers)] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ReviewFeedStructureOrLabel
        {
            get => (string)this[nameof(ReviewFeedStructureOrLabel)];
            set => this[nameof(ReviewFeedStructureOrLabel)] = value;
        }

        /// <summary>
        /// Section numbers/ranges for the Review Changes tab. Blank means the whole volume.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ReviewFeedSections
        {
            get => (string)this[nameof(ReviewFeedSections)];
            set => this[nameof(ReviewFeedSections)] = value;
        }
    }
}
