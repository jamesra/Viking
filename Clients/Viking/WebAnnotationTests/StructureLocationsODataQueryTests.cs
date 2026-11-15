using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.StructureLocations;

namespace WebAnnotationTests
{
    /// <summary>
    /// The Locations tab's OData query: table columns only (no shapes), the structure's rows, and pages followed by their
    /// next link.
    /// </summary>
    [TestClass]
    public class StructureLocationsODataQueryTests
    {
        [TestMethod]
        public void TheQueryAsksForOneStructuresTableColumnsAndNoShapes()
        {
            Uri uri = StructureLocationsODataQuery.BuildUri(new Uri("https://host.example/RC1/OData/"), 476);
            string query = Uri.UnescapeDataString(uri.Query);

            Assert.AreEqual("/RC1/OData/Locations", uri.AbsolutePath);
            StringAssert.Contains(query, "$filter=ParentID eq 476");
            StringAssert.Contains(query, "$select=" + StructureLocationsODataQuery.SelectedColumns);
            StringAssert.Contains(query, "$expand=LocationLinksA($select=A),LocationLinksB($select=B)");
            Assert.IsFalse(query.Contains("Shape"), "no geometry is downloaded");
        }

        [TestMethod]
        public void APageGivesItsRowsAndTheLinkToTheNextPage()
        {
            const string json = """
                {
                  "@odata.context": "https://host.example/RC1/OData/$metadata#Locations",
                  "value": [
                    { "ID": 10, "ParentID": 476, "X": 1.5, "Y": 2.5, "Z": 7, "VolumeX": 3.5, "VolumeY": 4.5, "Radius": 20,
                      "Width": null, "TypeCode": 1, "Terminal": false, "OffEdge": false, "Tags": null, "Username": "ann",
                      "LastModified": "2026-11-14T10:20:30Z",
                      "LocationLinksA": [ { "A": 10 } ], "LocationLinksB": [] },
                    { "ID": 11, "ParentID": 476, "X": 5, "Y": 6, "Z": 8, "VolumeX": 0, "VolumeY": 0, "Radius": 0,
                      "Width": 4, "TypeCode": 3, "Terminal": true, "OffEdge": false, "Tags": null, "Username": "bob",
                      "LastModified": "2026-11-13T00:00:00-07:00",
                      "LocationLinksA": [ { "A": 11 } ], "LocationLinksB": [ { "B": 11 } ] },
                    { "ParentID": 476 }
                  ],
                  "@odata.nextLink": "https://host.example/RC1/OData/Locations?$skiptoken=ID-11"
                }
                """;

            List<StructureLocationRow> rows = StructureLocationsODataQuery.ParsePage(json, out Uri? next);

            Assert.AreEqual(2, rows.Count, "a row without an ID is skipped");
            Assert.AreEqual(new Uri("https://host.example/RC1/OData/Locations?$skiptoken=ID-11"), next);

            StructureLocationRow first = rows[0];
            Assert.AreEqual(10L, first.ID);
            Assert.AreEqual(1.5, first.X);
            Assert.AreEqual(2.5, first.Y);
            Assert.AreEqual(7, first.Section);
            Assert.AreEqual(20.0, first.Radius);
            Assert.AreEqual(0.0, first.Width, "a missing width shows as 0, as before");
            Assert.AreEqual(LocationType.CIRCLE, first.TypeCode);
            Assert.AreEqual("ann", first.Username);
            Assert.AreEqual(new DateTime(2026, 11, 14, 10, 20, 30, DateTimeKind.Utc), first.LastModified);
            Assert.IsTrue(first.IsUnverifiedTerminal, "one link and no end mark");

            StructureLocationRow second = rows[1];
            Assert.AreEqual(new DateTime(2026, 11, 13, 7, 0, 0, DateTimeKind.Utc), second.LastModified, "times are shown in UTC");
            Assert.IsFalse(second.IsUnverifiedTerminal, "marked terminal");
        }

        [TestMethod]
        public void TheLastPageHasNoNextLink()
        {
            List<StructureLocationRow> rows = StructureLocationsODataQuery.ParsePage("""{ "value": [] }""", out Uri? next);

            Assert.AreEqual(0, rows.Count);
            Assert.IsNull(next);
        }

        [TestMethod]
        public void AnEndMarkedByAnAttributeOrTwoLinksIsNotUnverified()
        {
            const string json = """
                { "value": [
                  { "ID": 1, "TypeCode": 1, "Tags": "Untraceable", "LocationLinksA": [], "LocationLinksB": [] },
                  { "ID": 2, "TypeCode": 1, "LocationLinksA": [ { "A": 2 } ], "LocationLinksB": [ { "B": 2 } ] },
                  { "ID": 3, "TypeCode": 1, "Tags": "<not xml", "LocationLinksA": [], "LocationLinksB": [] }
                ] }
                """;

            List<StructureLocationRow> rows = StructureLocationsODataQuery.ParsePage(json, out _);

            Assert.IsFalse(rows[0].IsUnverifiedTerminal, "untraceable");
            Assert.IsFalse(rows[1].IsUnverifiedTerminal, "two links");
            Assert.IsTrue(rows[2].IsUnverifiedTerminal, "tags that do not parse count as none, and the row still lists");
        }
    }
}
