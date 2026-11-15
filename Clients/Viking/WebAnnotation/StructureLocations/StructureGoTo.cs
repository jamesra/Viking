using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Geometry;
using Newtonsoft.Json.Linq;
using Viking.VolumeModel;
using WebAnnotationModel;
using static WebAnnotation.StructureLocations.ODataPages;

namespace WebAnnotation.StructureLocations
{
    /// <summary>
    /// Goes to one location of a structure (Go to Structure, and double-clicking a structure in a property tab's list) without
    /// downloading the structure's shapes. The positions of its locations come from OData (<see cref="PositionsUri"/>: ID,
    /// mosaic X, Y, Z and radius), each is mapped into the volume by the open volume's transform for its section
    /// (<see cref="LocationVolumeGeometry.TryMapPoints"/>), a location is picked, and only that location is loaded, then
    /// shown. The downloads and mapping run off the UI thread; a newer go-to replaces one still waiting.
    /// </summary>
    public static class StructureGoTo
    {
        /// <summary>A location's position: its section and where its mosaic centre maps in the volume.</summary>
        public readonly record struct Placed(long ID, Vector2 Volume, double Z, double Radius);

        /// <summary>Counts go-tos; only the newest one moves the view.</summary>
        private static int latest;

        /// <summary>The structure's locations with their ID, mosaic centre, section and mosaic radius only.</summary>
        public static Uri PositionsUri(Uri odataRoot, long structureID) =>
            new(odataRoot.AbsoluteUri.TrimEnd('/') + "/Locations?$filter=" + EqualsFilter("ParentID", structureID)
                + "&$select=ID,X,Y,Z,Radius&$orderby=ID");

        private record Position(long ID, Vector2 Mosaic, double Z, double Radius);

        private static Position? ParsePosition(JToken value)
        {
            if (!TryReadInt64(Prop(value, "ID"), out long id) || id <= 0
                || !TryReadDouble(Prop(value, "X"), out double x) || !TryReadDouble(Prop(value, "Y"), out double y)
                || !TryReadDouble(Prop(value, "Z"), out double z))
            {
                return null;
            }

            TryReadDouble(Prop(value, "Radius"), out double radius);
            return new Position(id, new Vector2(x, y), z, radius);
        }

        /// <summary>
        /// The location nearest <paramref name="origin"/> (volume coordinates times <paramref name="scale"/>, as
        /// <c>Global.Scale</c> gives them); null when there is none. Go to Structure starts from the last mouse-down point.
        /// </summary>
        public static long? Nearest(IReadOnlyList<Placed> placed, Vector3 origin, Vector3 scale)
        {
            if (placed.Count == 0)
                return null;

            return placed.OrderBy(p => Vector3.Distance(Scaled(p, scale), origin)).First().ID;
        }

        /// <summary>
        /// The location nearest the structure's middle: the mean of its positions weighted by radius squared, so large
        /// locations count more (all equally when no location has a radius); null when there is none.
        /// </summary>
        public static long? Center(IReadOnlyList<Placed> placed, Vector3 scale)
        {
            if (placed.Count == 0)
                return null;

            double weightSum = placed.Sum(p => p.Radius * p.Radius);
            Func<Placed, double> weight = weightSum > 0 ? p => p.Radius * p.Radius : _ => 1.0;
            if (weightSum <= 0)
                weightSum = placed.Count;

            Vector3 mean = new(
                placed.Sum(p => p.Volume.X * weight(p)) / weightSum * scale.X,
                placed.Sum(p => p.Volume.Y * weight(p)) / weightSum * scale.Y,
                placed.Sum(p => p.Z * weight(p)) / weightSum * scale.Z);

            return placed.OrderBy(p => Vector3.Distance(Scaled(p, scale), mean)).First().ID;
        }

        private static Vector3 Scaled(Placed p, Vector3 scale) => new(p.Volume.X * scale.X, p.Volume.Y * scale.Y, p.Z * scale.Z);

        /// <summary>The scale of volume X, Y and section Z that Viking measures distances in.</summary>
        public static Vector3 GlobalScale => Global.Scale;

        /// <summary>
        /// Maps each position's mosaic centre into the volume with its section's transform, a section at a time. A location
        /// whose centre does not map is left out, as the old go-to ranked it last. With no volume open the mosaic centre is
        /// used, as the viewer then draws mosaic as volume. When nothing maps, every location is kept at its mosaic centre so
        /// the go-to still lands on one.
        /// </summary>
        private static List<Placed> Place(IReadOnlyList<Position> positions, IVolumeTransformProvider? volume)
        {
            List<Placed> placed = new(positions.Count);
            if (volume is null)
            {
                placed.AddRange(positions.Select(p => new Placed(p.ID, p.Mosaic, p.Z, p.Radius)));
                return placed;
            }

            foreach (IGrouping<int, Position> section in positions.GroupBy(p => (int)Math.Round(p.Z)))
            {
                Position[] onSection = [.. section];
                IVolumeToSectionTransform? transform = null;
                try
                {
                    transform = volume.GetSectionToVolumeTransform(section.Key);
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"No transform for section {section.Key} to place a structure's locations: {e.Message}");
                }

                if (transform is null)
                    continue;

                bool[] mapped = LocationVolumeGeometry.TryMapPoints([.. onSection.Select(p => p.Mosaic)], transform, out Vector2[] volumePoints);
                for (int i = 0; i < onSection.Length; i++)
                {
                    if (mapped[i])
                        placed.Add(new Placed(onSection[i].ID, volumePoints[i], onSection[i].Z, onSection[i].Radius));
                }
            }

            if (placed.Count == 0)
                placed.AddRange(positions.Select(p => new Placed(p.ID, p.Mosaic, p.Z, p.Radius)));

            return placed;
        }

        /// <summary>
        /// Goes to the location of <paramref name="structureID"/> that <paramref name="pick"/> chooses from the placed
        /// locations. Call it on the UI thread; it returns at once and the view moves when the server has answered. A
        /// structure with no locations, or a failed download, is reported in a message.
        /// </summary>
        public static async Task GoToAsync(long structureID, Func<IReadOnlyList<Placed>, long?> pick)
        {
            int goTo = Interlocked.Increment(ref latest);
            try
            {
                if (!TryRoot(out Uri root))
                {
                    MessageBox.Show($"Could not go to structure {structureID}: this annotation server has no OData service to find its locations.",
                        "Go to Structure", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                IVolumeTransformProvider? volume = LocationVolumeGeometryExtensions.Provider();
                Uri query = PositionsUri(root, structureID);
                List<Placed> placed = await Task.Run(async () =>
                    Place(await LoadAllAsync(query, ParsePosition, CancellationToken.None).ConfigureAwait(false), volume));

                if (goTo != latest)
                    return;

                if (pick(placed) is not long locationID)
                {
                    MessageBox.Show($"Structure {structureID} has no locations to go to.", "Go to Structure",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                LocationObj? location = await Task.Run(() => Store.Locations.GetObjectByID(locationID));
                if (goTo != latest)
                    return;

                if (location is null)
                {
                    MessageBox.Show($"Could not load location {locationID} of structure {structureID}.", "Go to Structure",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                AnnotationOverlay.GoToLocation(location);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Could not go to structure {structureID}: {e}");
                if (goTo == latest)
                {
                    MessageBox.Show($"Could not go to structure {structureID}: {Reason(e)}.", "Go to Structure",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
