using System;
using System.Configuration;
using System.Diagnostics;

namespace WebAnnotation.Properties
{
    internal sealed partial class Settings
    {
        /// <summary>
        /// Width in screen pixels of the narrowest part of a link line between two locations on different sections, 2 to 32.
        /// Keep the default in step with
        /// <see cref="WebAnnotation.Global.AnnotationSettings.DEFAULT_LOCATION_LINK_WAIST_PIXELS"/>.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("8")]
        public int LocationLinkWaistPixels
        {
            get => (int)this[nameof(LocationLinkWaistPixels)];
            set => this[nameof(LocationLinkWaistPixels)] = value;
        }

        /// <summary>
        /// True also draws a location link under its two location markers, as a round end the size of each marker.
        /// False draws nothing under the markers.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool LocationLinkDrawUnderMarkers
        {
            get => (bool)this[nameof(LocationLinkDrawUnderMarkers)];
            set => this[nameof(LocationLinkDrawUnderMarkers)] = value;
        }

        /// <summary>
        /// Seconds an annotation hidden with the Del key stays hidden before it reappears, 1 to 600.
        /// Keep the default in step with <see cref="WebAnnotation.Global.AnnotationSettings.DEFAULT_HIDE_ANNOTATION_SECONDS"/>.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("30")]
        public int HideAnnotationSeconds
        {
            get => (int)this[nameof(HideAnnotationSeconds)];
            set => this[nameof(HideAnnotationSeconds)] = value;
        }
    }
}
