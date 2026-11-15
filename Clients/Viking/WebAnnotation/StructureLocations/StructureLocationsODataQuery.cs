using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using static WebAnnotation.StructureLocations.ODataPages;

namespace WebAnnotation.StructureLocations
{
    /// <summary>
    /// Builds and reads the OData query behind a structure's Locations tab: one row per location of the structure with the
    /// table's columns only, never the mosaic or volume shapes, so a large cell lists quickly on a slow connection. Pages are
    /// read with <see cref="ODataPages"/>; the tab shows each one as it arrives. Geometry is fetched for one location when the
    /// user acts on its row (<see cref="StructureLocationRow"/>).
    /// </summary>
    public static class StructureLocationsODataQuery
    {
        /// <summary>
        /// The columns the table shows, plus <c>Tags</c> (the attributes that mark a varicosity cap or untraceable end) for
        /// the IsUnverifiedTerminal column. No shape columns.
        /// </summary>
        public const string SelectedColumns =
            "ID,ParentID,X,Y,Z,VolumeX,VolumeY,Radius,Width,TypeCode,Terminal,OffEdge,Tags,Username,LastModified";

        /// <summary>The link ends only, to count each location's links for the IsUnverifiedTerminal column.</summary>
        public const string ExpandedLinks = "LocationLinksA($select=A),LocationLinksB($select=B)";

        /// <summary>
        /// <c>Locations</c> of <paramref name="structureID"/> ordered by section then ID, with <see cref="SelectedColumns"/>
        /// and <see cref="ExpandedLinks"/>.
        /// </summary>
        public static Uri BuildUri(Uri odataRoot, long structureID)
        {
            if (odataRoot is null)
                throw new ArgumentNullException(nameof(odataRoot));

            return new Uri(odataRoot.AbsoluteUri.TrimEnd('/') + "/Locations?$filter=" + EqualsFilter("ParentID", structureID)
                + "&$select=" + SelectedColumns
                + "&$expand=" + ExpandedLinks
                + "&$orderby=Z,ID");
        }

        /// <summary>One response's rows (a row without an ID is skipped) and the link to the next page, null on the last.</summary>
        public static List<StructureLocationRow> ParsePage(string json, out Uri? nextLink) =>
            ODataPages.ParsePage(json, ParseRow, out nextLink);

        private static StructureLocationRow? ParseRow(JToken value)
        {
            if (!TryReadInt64(Prop(value, "ID"), out long id) || id <= 0)
                return null;

            TryReadInt64(Prop(value, "ParentID"), out long parentID);
            TryReadDouble(Prop(value, "X"), out double x);
            TryReadDouble(Prop(value, "Y"), out double y);
            TryReadDouble(Prop(value, "Z"), out double z);
            TryReadDouble(Prop(value, "VolumeX"), out double volumeX);
            TryReadDouble(Prop(value, "VolumeY"), out double volumeY);
            TryReadDouble(Prop(value, "Radius"), out double radius);
            TryReadDouble(Prop(value, "Width"), out double width);
            TryReadInt64(Prop(value, "TypeCode"), out long typeCode);

            List<ObjAttribute> attributes = ParseTags(ReadString(Prop(value, "Tags")));
            bool varicosityCap = attributes.Exists(a => a.Name == "Varicosity Cap");
            bool untraceable = attributes.Exists(a => a.Name == "Untraceable");

            int links = CountOf(Prop(value, "LocationLinksA")) + CountOf(Prop(value, "LocationLinksB"));

            return new StructureLocationRow(
                id,
                parentID,
                x,
                y,
                z,
                volumeX,
                volumeY,
                width,
                radius,
                (LocationType)typeCode,
                ReadBool(Prop(value, "Terminal")),
                ReadBool(Prop(value, "OffEdge")),
                varicosityCap,
                untraceable,
                links,
                ReadString(Prop(value, "Username")) ?? "",
                ReadUtc(Prop(value, "LastModified")));
        }
    }
}
