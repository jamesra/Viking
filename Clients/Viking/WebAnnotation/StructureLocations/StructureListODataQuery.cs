using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using WebAnnotationModel;
using static WebAnnotation.StructureLocations.ODataPages;

namespace WebAnnotation.StructureLocations
{
    /// <summary>
    /// Builds and reads the OData queries behind the structure lists in the property tabs: a structure's children (Child
    /// Structures tab) and every structure of a type (a structure type's Structures tab). One row per structure with the
    /// table's columns and its structure link ends (for Num Links), read a page at a time with <see cref="ODataPages"/>.
    /// The whole structure is loaded only when the user acts on a row (<see cref="StructureRow"/>).
    /// </summary>
    public static class StructureListODataQuery
    {
        /// <summary>The columns the table shows (Tags are its Attributes column).</summary>
        public const string SelectedColumns = "ID,TypeID,ParentID,Label,Username,Verified,Confidence,Tags,Notes";

        /// <summary>The structure link ends only, to count each structure's links for the Num Links column.</summary>
        public const string ExpandedLinks = "SourceOfLinks($select=SourceID),TargetOfLinks($select=TargetID)";

        /// <summary>The children of <paramref name="parentID"/>, ordered by ID.</summary>
        public static Uri ChildrenUri(Uri odataRoot, long parentID) => Build(odataRoot, "ParentID", parentID);

        /// <summary>The structures of type <paramref name="typeID"/>, ordered by ID.</summary>
        public static Uri OfTypeUri(Uri odataRoot, long typeID) => Build(odataRoot, "TypeID", typeID);

        private static Uri Build(Uri odataRoot, string property, long value)
        {
            if (odataRoot is null)
                throw new ArgumentNullException(nameof(odataRoot));

            return new Uri(odataRoot.AbsoluteUri.TrimEnd('/') + "/Structures?$filter=" + EqualsFilter(property, value)
                + "&$select=" + SelectedColumns
                + "&$expand=" + ExpandedLinks
                + "&$orderby=ID");
        }

        /// <summary>One response's rows (a row without an ID is skipped) and the link to the next page, null on the last.</summary>
        public static List<StructureRow> ParsePage(string json, out Uri? nextLink) => ODataPages.ParsePage(json, ParseRow, out nextLink);

        private static StructureRow? ParseRow(JToken value)
        {
            if (!TryReadInt64(Prop(value, "ID"), out long id) || id <= 0)
                return null;

            TryReadInt64(Prop(value, "TypeID"), out long typeID);
            long? parentID = TryReadInt64(Prop(value, "ParentID"), out long parent) ? parent : null;
            TryReadDouble(Prop(value, "Confidence"), out double confidence);
            List<ObjAttribute> attributes = ParseTags(ReadString(Prop(value, "Tags")));
            int links = CountOf(Prop(value, "SourceOfLinks")) + CountOf(Prop(value, "TargetOfLinks"));

            return new StructureRow(
                id,
                typeID,
                parentID,
                ReadString(Prop(value, "Label")) ?? "",
                ReadString(Prop(value, "Username")) ?? "",
                links,
                ReadBool(Prop(value, "Verified")),
                confidence,
                attributes.ToArray(),
                ReadString(Prop(value, "Notes")) ?? "");
        }
    }
}
