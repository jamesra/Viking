using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlGeometryUtils;
using Annotation;

namespace AnnotationService.Types
{
    public static class LocationPositionOnlyExtensions
    {
        public static LocationPositionOnly Create(this ConnectomeDataModel.SelectUnfinishedStructureBranchesWithPosition_Result db)
        {
            LocationPositionOnly lpo = new LocationPositionOnly();
            lpo.ID = db.ID;
            lpo.Position = new AnnotationPoint(db.X, db.Y, (double)db.Z);
            lpo.Radius = db.Radius;
            return lpo;
        }

        public static LocationPositionOnly CreatePositionOnly(this ConnectomeDataModel.Location db)
        {
            LocationPositionOnly lpo = new LocationPositionOnly();
            lpo.ID = db.ID;
            lpo.Position = new AnnotationPoint(db.X, db.Y, db.Z);
            lpo.Radius = db.Radius;
            return lpo;
        }
    }

    public static class LocationExtensions
    {
        public static Location Create(this ConnectomeDataModel.Location db, bool LoadLinks = false)
        {
            Location loc = new Location();
            loc.ID = db.ID;

            loc.ParentID = db.ParentID;

            loc.Section = (long)db.Z;
            loc.Position = new AnnotationPoint(db.X, db.Y, (int)db.Z);
            loc.VolumePosition = new AnnotationPoint(db.VolumeX, db.VolumeY, (int)db.Z);
            loc.MosaicShape = db.MosaicShape;
            loc.VolumeShape = db.VolumeShape;
            loc.Closed = db.Closed;
            if (LoadLinks)
                loc.PopulateLinks(db);

            loc.Terminal = db.Terminal;
            loc.OffEdge = db.OffEdge;
            loc.TypeCode = db.TypeCode;
            loc.Radius = db.Radius;
            loc.Width = db.Width;

            if (db.Tags == null)
            {
                //_Tags = new string[0];
                loc.AttributesXml = null;
            }
            else
            {
                //    _Tags = db.Tags.Split(';');
                loc.AttributesXml = db.Tags;
            }

            loc.LastModified = db.LastModified.Ticks;
            loc.Username = db.Username;

            return loc;
        }

        public static void Sync(this Location loc, ConnectomeDataModel.Location db)
        {
            //I want to update VolumeX and VolumeY with the viking client, but I don't want to 
            //write all the code for a server utility to update it manually.  So if the only column changing is 
            //VolumeX and VolumeY we do not update the username field.  Currently if I regenerate the volume transforms the
            //next client to run viking would plaster the username history if I did not do this.
            bool UpdateUserName = false;

            UpdateUserName |= db.ParentID != loc.ParentID;
            db.ParentID = loc.ParentID;

            UpdateUserName |= db.X != loc.Position.X;
            //db.X = loc.Position.X;

            UpdateUserName |= db.Y != loc.Position.Y;
            //db.Y = loc.Position.Y;

            UpdateUserName |= db.Z != loc.Position.Z;
            db.Z = (int)loc.Position.Z;

            if (loc.MosaicShape == null)
            {
                System.Data.Entity.Spatial.DbGeometry new_geom = loc.Radius > 0 ? Extensions.ToCircle(loc.Position.X, loc.Position.Y, loc.Position.Z, loc.Radius).ToDbGeometry() :
                                                                                  Extensions.ToGeometryPoint(loc.Position.X, loc.Position.Y).ToDbGeometry();

                UpdateUserName |= db.MosaicShape == null ? true : !db.MosaicShape.SpatialEquals(loc.MosaicShape);
                db.MosaicShape = new_geom;
            }
            else
            {
                UpdateUserName |= db.MosaicShape == null ? true : !db.MosaicShape.SpatialEquals(loc.MosaicShape);
                db.MosaicShape = loc.MosaicShape;
            }

            //See above comment before adding UpdateUserName test...
            //UpdateUserName |= db.VolumeShape != loc.VolumeShape;
            if (loc.VolumeShape == null)
                db.VolumeShape = loc.Radius > 0 ? Extensions.ToCircle(loc.VolumePosition.X, loc.VolumePosition.Y, loc.VolumePosition.Z, loc.Radius).ToDbGeometry() :
                                                  Extensions.ToGeometryPoint(loc.VolumePosition.X, loc.VolumePosition.Y).ToDbGeometry();
            else
                db.VolumeShape = loc.VolumeShape;

            //See above comment before adding UpdateUserName test...
            //db.VolumeX = loc.VolumePosition.X;
            //db.VolumeY = loc.VolumePosition.Y;


            UpdateUserName |= db.Closed != loc.Closed;
            db.Closed = loc.Closed;

            //Update the tags
            if (db.Tags != null)
                if (db.Tags != loc.AttributesXml)
                    if (!(db.Tags.Length <= 1 && loc.AttributesXml.Length <= 1))
                        UpdateUserName = true;

            if (string.IsNullOrWhiteSpace(loc.AttributesXml))
                db.Tags = null;
            else
                db.Tags = loc.AttributesXml;

            UpdateUserName |= db.Terminal != loc.Terminal;
            db.Terminal = loc.Terminal;

            UpdateUserName |= db.OffEdge != loc.OffEdge;
            db.OffEdge = loc.OffEdge;

            UpdateUserName |= db.TypeCode != loc.TypeCode;
            db.TypeCode = loc.TypeCode;

            UpdateUserName |= db.Radius != loc.Radius;
            db.Radius = loc.Radius;

            UpdateUserName |= db.Width != loc.Width;
            db.Width = loc.Width;

            UpdateUserName |= db.Username == null;

            if (UpdateUserName)
            {
                db.Username = Annotation.ServiceModelUtil.GetUserForCall();
            }
            else if (db.Username == null)
            {
                if (loc.Username != null)
                    db.Username = loc.Username;
                else
                    db.Username = Annotation.ServiceModelUtil.GetUserForCall();
            }
        }

        public static void PopulateLinks(Dictionary<long, Location> Locations, IList<ConnectomeDataModel.LocationLink> links)
        {
            Location A;
            Location B;
            foreach (ConnectomeDataModel.LocationLink link in links)
            {
                if (Locations.TryGetValue(link.A, out A))
                {
                    A.AddLink(link.B);
                }

                if (Locations.TryGetValue(link.B, out B))
                {
                    B.AddLink(link.A);
                }
            }
        }

        /// <summary>
        /// Populates the links array using relations from the database
        /// </summary>
        /// <param name="dbLoc"></param>
        /// <returns></returns>
        private static void PopulateLinks(this Location loc, ConnectomeDataModel.Location dbLoc)
        {
            if (!(dbLoc.LocationLinksA.Any() || dbLoc.LocationLinksB.Any()))
                return;

            //long[] _Links = new long[loc.LocationLinksA.Count + loc.LocationLinksB.Count];
            List<long> retlist = new List<long>(dbLoc.LocationLinksA.Count + dbLoc.LocationLinksB.Count);

            retlist.AddRange(dbLoc.LocationLinksA.Select(l => l.B).ToList());
            retlist.AddRange(dbLoc.LocationLinksB.Select(l => l.A).ToList());

            loc.Links = retlist.ToArray();
        }
    }

    public static class LocationHistoryExtensions
    {
        public static LocationHistory Create(this ConnectomeDataModel.SelectStructureLocationChangeLog_Result db)
        {
            LocationHistory loch = new LocationHistory();
            loch.ID = db.ID.Value;
            loch.ParentID = db.ParentID.Value;

            loch.Section = (long)db.Z;
            if (db.X != null && db.Y != null)
            {
                loch.Position = new AnnotationPoint(db.X.Value, db.Y.Value, db.Z.Value);
            }
            else
            {
                loch.Position = new AnnotationPoint(double.NaN, double.NaN, db.Z.Value);
            }

            if (db.VolumeX != null && db.VolumeY != null)
            {
                loch.VolumePosition = new AnnotationPoint(db.VolumeX.Value, db.VolumeY.Value, db.Z.Value);
            }
            else
            {
                loch.VolumePosition = new AnnotationPoint(double.NaN, double.NaN, db.Z.Value);
            }

            loch.Closed = db.Closed.Value;
            loch.Links = null;
            loch.Terminal = db.Terminal.Value;
            loch.OffEdge = db.OffEdge.Value;
            loch.TypeCode = db.TypeCode.Value;
            loch.Radius = db.Radius.Value;
            loch.ChangedColumnMask = 0; //TODO: System.Convert.ToUInt64(db.___update_mask); 


            if (db.Tags == null)
            {
                //_Tags = new string[0];
                loch.AttributesXml = null;
            }
            else
            {
                loch.AttributesXml = db.Tags;
            }

            loch.LastModified = db.LastModified.Value.Ticks;
            loch.Username = db.Username;

            return loch;
        }
    }
}
