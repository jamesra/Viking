using System;
using System.Collections.Generic;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.StructureLocations;

namespace WebAnnotationTests
{
    /// <summary>
    /// The structure lists in the property tabs (children, a type's structures) and Go to Structure, all read from OData
    /// without shapes.
    /// </summary>
    [TestClass]
    public class StructureListODataQueryTests
    {
        private static readonly Uri Root = new("https://host.example/RC1/OData/");

        [TestMethod]
        public void ChildrenAndTypeListsFilterOnTheirOwnProperty()
        {
            string children = Uri.UnescapeDataString(StructureListODataQuery.ChildrenUri(Root, 476).Query);
            string ofType = Uri.UnescapeDataString(StructureListODataQuery.OfTypeUri(Root, 1).Query);

            StringAssert.Contains(children, "$filter=ParentID eq 476");
            StringAssert.Contains(ofType, "$filter=TypeID eq 1");
            foreach (string query in new[] { children, ofType })
            {
                StringAssert.Contains(query, "$select=" + StructureListODataQuery.SelectedColumns);
                StringAssert.Contains(query, "$expand=" + StructureListODataQuery.ExpandedLinks);
            }
        }

        [TestMethod]
        public void AStructureRowShowsItsColumnsAndCountsLinksBothWays()
        {
            const string json = """
                { "value": [
                  { "ID": 900, "TypeID": 34, "ParentID": 476, "Label": "ribbon", "Username": "ann", "Verified": true,
                    "Confidence": 0.5, "Tags": "Untraceable", "Notes": "check",
                    "SourceOfLinks": [ { "SourceID": 900 } ], "TargetOfLinks": [ { "TargetID": 900 }, { "TargetID": 900 } ] },
                  { "ID": 901, "TypeID": 1, "ParentID": null, "Label": null, "SourceOfLinks": [], "TargetOfLinks": [] }
                ],
                  "@odata.nextLink": "https://host.example/RC1/OData/Structures?$skiptoken=ID-901" }
                """;

            List<StructureRow> rows = StructureListODataQuery.ParsePage(json, out Uri? next);

            Assert.AreEqual(2, rows.Count);
            Assert.IsNotNull(next);
            Assert.AreEqual("900", rows[0].ToString());
            Assert.AreEqual(476L, rows[0].ParentID);
            Assert.AreEqual("ribbon", rows[0].InfoLabel);
            Assert.AreEqual(3, rows[0].NumLinks);
            Assert.IsTrue(rows[0].Verified);
            Assert.AreEqual(0.5, rows[0].Confidence);
            Assert.AreEqual("check", rows[0].Notes);
            Assert.AreEqual(1, rows[0].AttributeList.Length);
            Assert.IsNull(rows[1].ParentID);
            Assert.AreEqual("", rows[1].InfoLabel);
            Assert.AreEqual(0, rows[1].NumLinks);
        }

        [TestMethod]
        public void GoToStructureAsksForPositionsOnly()
        {
            string query = Uri.UnescapeDataString(StructureGoTo.PositionsUri(Root, 476).Query);

            StringAssert.Contains(query, "$filter=ParentID eq 476");
            StringAssert.Contains(query, "$select=ID,X,Y,Z,Radius");
            Assert.IsFalse(query.Contains("Shape"));
        }

        [TestMethod]
        public void GoToStructurePicksTheLocationNearestTheOrigin()
        {
            Vector3 scale = new(2, 2, 90);
            StructureGoTo.Placed[] placed =
            [
                new(1, new Vector2(0, 0), 10, 5),
                new(2, new Vector2(100, 100), 12, 5),
                new(3, new Vector2(100, 100), 40, 5),
            ];

            Assert.AreEqual(2L, StructureGoTo.Nearest(placed, new Vector3(200, 200, 12 * 90), scale));
            Assert.AreEqual(1L, StructureGoTo.Nearest(placed, new Vector3(0, 0, 10 * 90), scale));
            Assert.IsNull(StructureGoTo.Nearest([], new Vector3(0, 0, 0), scale));
        }

        [TestMethod]
        public void TheMiddleOfAStructureWeightsLargeLocationsMore()
        {
            Vector3 scale = new(1, 1, 1);
            StructureGoTo.Placed[] placed =
            [
                new(1, new Vector2(0, 0), 0, 1),
                new(2, new Vector2(10, 0), 0, 10),
                new(3, new Vector2(20, 0), 0, 1),
            ];

            Assert.AreEqual(2L, StructureGoTo.Center(placed, scale), "the big middle location");

            StructureGoTo.Placed[] lopsided =
            [
                new(1, new Vector2(0, 0), 0, 10),
                new(2, new Vector2(9, 0), 0, 1),
                new(3, new Vector2(20, 0), 0, 1),
            ];
            Assert.AreEqual(1L, StructureGoTo.Center(lopsided, scale), "the mean is pulled to the large location");

            StructureGoTo.Placed[] noRadius =
            [
                new(1, new Vector2(0, 0), 0, 0),
                new(2, new Vector2(10, 0), 0, 0),
                new(3, new Vector2(20, 0), 0, 0),
            ];
            Assert.AreEqual(2L, StructureGoTo.Center(noRadius, scale), "without radii every location counts the same");
        }
    }
}
