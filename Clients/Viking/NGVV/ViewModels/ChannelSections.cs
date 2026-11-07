using System;
using System.Collections.Generic;
using Viking.VolumeModel;

namespace Viking.ViewModels
{
    /// <summary>
    /// Pairs the channels a section draws with the sections their images come from. Used by
    /// <see cref="SectionViewModel.ChannelSectionsToDraw"/>, which the section viewer's texture preload, Export Tiles
    /// check and capture wait enumerate.
    /// </summary>
    internal static class ChannelSections
    {
        /// <summary>
        /// Yields each channel with the section <paramref name="sectionFor"/> returns for it, in channel order, and
        /// skips a channel whose section is null. A null section is ordinary: an ABOVE or BELOW channel at the first or
        /// last section of a volume, or a FIXED channel naming a section the volume does not have. Drawing skips those
        /// channels, so everything that loads or waits for that channel's tiles must skip them too.
        /// </summary>
        /// <typeparam name="TSection">The section type; generic so tests need not build a <see cref="Volume"/>.</typeparam>
        /// <param name="channels">Channels to pair.</param>
        /// <param name="sectionFor">Section a channel draws from, or null when it has none. Called once per channel,
        /// lazily, as the result is enumerated.</param>
        internal static IEnumerable<(ChannelInfo Channel, TSection Section)> SkipMissing<TSection>(
            IEnumerable<ChannelInfo> channels, Func<ChannelInfo, TSection?> sectionFor)
            where TSection : class
        {
            foreach (ChannelInfo channel in channels)
            {
                TSection? section = sectionFor(channel);
                if (section is not null)
                    yield return (channel, section);
            }
        }
    }
}
