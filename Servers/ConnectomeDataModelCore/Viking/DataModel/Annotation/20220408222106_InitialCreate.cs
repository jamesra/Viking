using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

namespace Viking.DataModel.Annotation
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedLocations",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    DeletedOn = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedLocations", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "StructureType",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentID = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nchar(128)", fixedLength: true, maxLength: 128, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MarkupType = table.Column<string>(type: "nchar(16)", fixedLength: true, maxLength: 16, nullable: false, defaultValueSql: "(N'Point')", comment: "Point,Line,Poly"),
                    Tags = table.Column<string>(type: "xml", nullable: true, comment: "Strings seperated by semicolins"),
                    StructureTags = table.Column<string>(type: "xml", nullable: true),
                    Abstract = table.Column<bool>(type: "bit", nullable: false),
                    Color = table.Column<int>(type: "int", nullable: false, defaultValueSql: "(0xFFFFFF)"),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Code = table.Column<string>(type: "nchar(16)", fixedLength: true, maxLength: 16, nullable: false, defaultValueSql: "(N'No Code')", comment: "Code used to identify these items in the UI"),
                    HotKey = table.Column<string>(type: "char(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: false, defaultValueSql: "(N'\0')", comment: "Hotkey used to create a structure of this type"),
                    Username = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, defaultValueSql: "(N'')", comment: "Last username to modify the row"),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())"),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureType", x => x.ID);
                    table.ForeignKey(
                        name: "FK_StructureType_StructureType",
                        column: x => x.ParentID,
                        principalTable: "StructureType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermittedStructureLink",
                columns: table => new
                {
                    SourceTypeID = table.Column<long>(type: "bigint", nullable: false),
                    TargetTypeID = table.Column<long>(type: "bigint", nullable: false),
                    Bidirectional = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermittedStructureLink", x => new { x.SourceTypeID, x.TargetTypeID });
                    table.ForeignKey(
                        name: "FK_PermittedStructureLink_SourceType",
                        column: x => x.SourceTypeID,
                        principalTable: "StructureType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermittedStructureLink_TargetType",
                        column: x => x.TargetTypeID,
                        principalTable: "StructureType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Structure",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeID = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Verified = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "xml", nullable: true, comment: "Strings seperated by semicolins"),
                    Confidence = table.Column<double>(type: "float", nullable: false, defaultValueSql: "((0.5))", comment: "How certain is it that the structure is what we say it is"),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false, comment: "Records last write time"),
                    ParentID = table.Column<long>(type: "bigint", nullable: true, comment: "If the structure is contained in a larger structure (Synapse for a cell) this index contains the index of the parent"),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())", comment: "Date the structure was created"),
                    Label = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true, comment: "Additional Label for structure in UI"),
                    Username = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, defaultValueSql: "(N'')", comment: "Last username to modify the row"),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Structure", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Structure_Structure",
                        column: x => x.ParentID,
                        principalTable: "Structure",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StructureBase_StructureType",
                        column: x => x.TypeID,
                        principalTable: "StructureType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StructureTemplates",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, comment: "Name of template"),
                    StructureTypeID = table.Column<long>(type: "bigint", nullable: false, comment: "The structure type which is created when using the template"),
                    StructureTags = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "The tags to create with the new structure type"),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureTemplates", x => x.ID);
                    table.ForeignKey(
                        name: "FK_StructureTemplates_StructureType",
                        column: x => x.StructureTypeID,
                        principalTable: "StructureType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Location",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentID = table.Column<long>(type: "bigint", nullable: false, comment: "Structure which we belong to"),
                    Z = table.Column<long>(type: "bigint", nullable: false),
                    Closed = table.Column<bool>(type: "bit", nullable: false, comment: "Defines whether Vertices form a closed figure (The last vertex connects to the first)"),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Overlay = table.Column<byte[]>(type: "varbinary(max)", nullable: true, comment: "An image centered on X,Y,Z which specifies which surrounding pixels are part of location"),
                    Tags = table.Column<string>(type: "xml", nullable: true),
                    Terminal = table.Column<bool>(type: "bit", nullable: false, comment: "Set to true if this location is the edge of a structure and cannot be extended."),
                    OffEdge = table.Column<bool>(type: "bit", nullable: false, comment: "This bit is set if the structure leaves the volume at this location"),
                    TypeCode = table.Column<short>(type: "smallint", nullable: false, defaultValueSql: "((1))", comment: "0 = Point, 1 = Circle, 2=Ellipse, 3 =PolyLine, 4=Polygon"),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())", comment: "Date the location was last modified"),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())", comment: "Date the location was created"),
                    Username = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, defaultValueSql: "(N'')", comment: "Last username to modify the row"),
                    MosaicShape = table.Column<Geometry>(type: "geometry", nullable: false),
                    VolumeShape = table.Column<Geometry>(type: "geometry", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false, computedColumnSql: "(isnull([MosaicShape].[STCentroid]().STX,isnull([MosaicShape].[STX],(0))))", stored: true),
                    Y = table.Column<double>(type: "float", nullable: false, computedColumnSql: "(isnull([MosaicShape].[STCentroid]().STY,isnull([MosaicShape].[STY],(0))))", stored: true),
                    VolumeX = table.Column<double>(type: "float", nullable: false, computedColumnSql: "(isnull([VolumeShape].[STCentroid]().STX,isnull([VolumeShape].[STX],isnull([VolumeShape].[STEnvelope]().STCentroid().STX,(0)))))", stored: true),
                    VolumeY = table.Column<double>(type: "float", nullable: false, computedColumnSql: "(isnull([VolumeShape].[STCentroid]().STY,isnull([VolumeShape].[STY],isnull([VolumeShape].[STEnvelope]().STCentroid().STY,(0)))))", stored: true),
                    Width = table.Column<double>(type: "float", nullable: true, comment: "Width used for line annotation types"),
                    Radius = table.Column<double>(type: "float", nullable: false, computedColumnSql: "(case [MosaicShape].[STDimension]() when (0) then (0) when (1) then [MosaicShape].[STLength]()/(2.0) when (2) then sqrt([MosaicShape].[STArea]()/pi())  end)", stored: true, comment: "Radius, calculated column needed for backwards compatability")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Location", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Location_StructureBase1",
                        column: x => x.ParentID,
                        principalTable: "Structure",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StructureLink",
                columns: table => new
                {
                    SourceID = table.Column<long>(type: "bigint", nullable: false),
                    TargetID = table.Column<long>(type: "bigint", nullable: false),
                    Bidirectional = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "xml", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, defaultValueSql: "(N'')", comment: "Last username to modify the row"),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())", comment: "Row Creation Date"),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_StructureLinkSource_StructureBaseID",
                        column: x => x.SourceID,
                        principalTable: "Structure",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StructureLinkTarget_StructureBaseID",
                        column: x => x.TargetID,
                        principalTable: "Structure",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StructureSpatialCache",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    BoundingRect = table.Column<Geometry>(type: "geometry", nullable: false),
                    Area = table.Column<double>(type: "float", nullable: false),
                    Volume = table.Column<double>(type: "float", nullable: false),
                    MaxDimension = table.Column<int>(type: "int", nullable: false),
                    MinZ = table.Column<double>(type: "float", nullable: false),
                    MaxZ = table.Column<double>(type: "float", nullable: false),
                    ConvexHull = table.Column<Geometry>(type: "geometry", nullable: true),
                    LastModified = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureSpatialCache", x => x.ID);
                    table.ForeignKey(
                        name: "FK__Structure__LastM__1F959DAB",
                        column: x => x.ID,
                        principalTable: "Structure",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationLink",
                columns: table => new
                {
                    A = table.Column<long>(type: "bigint", nullable: false, comment: "The convention is that A is always less than B"),
                    B = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, defaultValueSql: "(N'')", comment: "Last username to modify the row"),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())", comment: "Row Creation Date")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationLink", x => new { x.A, x.B });
                    table.ForeignKey(
                        name: "FK_LocationLink_Location",
                        column: x => x.A,
                        principalTable: "Location",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationLink_Location1",
                        column: x => x.B,
                        principalTable: "Location",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "DeletedOn",
                table: "DeletedLocations",
                column: "DeletedOn")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "LastModified",
                table: "Location",
                column: "LastModified")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "ParentID",
                table: "Location",
                column: "ParentID")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "Z",
                table: "Location",
                column: "Z");

            migrationBuilder.CreateIndex(
                name: "a",
                table: "LocationLink",
                column: "A")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "b",
                table: "LocationLink",
                column: "B")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "LocationLink_A_B_Username_Created",
                table: "LocationLink",
                columns: new[] { "A", "B" });

            migrationBuilder.CreateIndex(
                name: "LocationLink_B_A_Username_Created",
                table: "LocationLink",
                columns: new[] { "B", "A" });

            migrationBuilder.CreateIndex(
                name: "IX_PermittedStructureLink_TargetTypeID",
                table: "PermittedStructureLink",
                column: "TargetTypeID");

            migrationBuilder.CreateIndex(
                name: "PermittedStructureLink_source_target_unique",
                table: "PermittedStructureLink",
                columns: new[] { "SourceTypeID", "TargetTypeID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "LastModified1",
                table: "Structure",
                column: "LastModified")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "ParentID1",
                table: "Structure",
                column: "ParentID")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "Structure_ParentID_ID",
                table: "Structure",
                columns: new[] { "ParentID", "ID" });

            migrationBuilder.CreateIndex(
                name: "TypeID",
                table: "Structure",
                column: "TypeID")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "source_target_unique",
                table: "StructureLink",
                columns: new[] { "SourceID", "TargetID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "SourceID",
                table: "StructureLink",
                column: "SourceID")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "TargetID",
                table: "StructureLink",
                column: "TargetID")
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "IX_StructureTemplates_StructureTypeID",
                table: "StructureTemplates",
                column: "StructureTypeID");

            migrationBuilder.CreateIndex(
                name: "ParentID2",
                table: "StructureType",
                column: "ParentID")
                .Annotation("SqlServer:FillFactor", 90);
            
            migrationBuilder.Sql(
                @"CREATE TRIGGER [dbo].[Location_update]  
                ON[dbo].[Location] 
                FOR UPDATE 
                AS 
                    SET NOCOUNT ON;
                    Update dbo.Location 
                    Set LastModified = (GETUTCDATE())  
                    WHERE ID in (SELECT ID FROM inserted)");

            migrationBuilder.Sql(
                @"CREATE TRIGGER [dbo].[Location_delete] 
	               ON  [dbo].[Location]
	               FOR DELETE
	             AS 
                    SET NOCOUNT ON;
		            INSERT INTO [dbo].[DeletedLocations] (ID)
		            SELECT deleted.ID FROM deleted
		            
		            delete from LocationLink 
			            where A in  (SELECT deleted.ID FROM deleted)
				            or B in (SELECT deleted.ID FROM deleted)");

            migrationBuilder.Sql(
                @"CREATE TRIGGER [dbo].[StructureLink_ReciprocalCheck] 
                    ON  [dbo].[StructureLink]
                    AFTER INSERT, UPDATE
                    AS 
	                    IF ((select count(SLA.SourceID)
		                    from inserted SLA 
		                    JOIN StructureLink SLB 
		                    ON (SLA.SourceID = SLB.TargetID AND SLA.TargetID = SLB.SourceID)) > 0)
		                    BEGIN
			                    RAISERROR(N'Reciprocal structure links are not allowed. Set the bidirectional property on the link instead.',14,1);
			                    ROLLBACK TRANSACTION;
			                    RETURN
		                    END");

            migrationBuilder.Sql(
                @"CREATE TRIGGER [dbo].[StructureType_LastModified] 
	               ON  [dbo].[StructureType]
	               FOR UPDATE
	            AS 
                    -- SET NOCOUNT ON added to prevent extra result sets from
		            -- interfering with SELECT statements.
                    SET NOCOUNT ON;
		            Update dbo.[StructureType]
		            Set LastModified = (SYSUTCDATETIME())
		            WHERE ID in (SELECT ID FROM inserted)
		            
		            ");

            migrationBuilder.Sql(
                @"CREATE TRIGGER [dbo].[Structure_LastModified] 
	               ON  [dbo].[Structure]
	               FOR UPDATE
	            AS 
                    -- SET NOCOUNT ON added to prevent extra result sets from
		            -- interfering with SELECT statements.
		            SET NOCOUNT ON;
		            Update dbo.[Structure]
		            Set LastModified = (SYSUTCDATETIME())
		            WHERE ID in (SELECT ID FROM inserted)
		            ");

            migrationBuilder.Sql(
                @"  CREATE SPATIAL INDEX [MosaicShape_Index] ON [dbo].[Location]
                    (
	                    [MosaicShape]
                    )USING  GEOMETRY_AUTO_GRID 
                    WITH (BOUNDING_BOX =(0, 0, 150000, 150000), 
                    CELLS_PER_OBJECT = 16, PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
                    GO");

            migrationBuilder.Sql(
                @"  CREATE SPATIAL INDEX [VolumeShape_Index] ON [dbo].[Location]
                    (
	                    [VolumeShape]
                    )USING  GEOMETRY_AUTO_GRID 
                    WITH (BOUNDING_BOX =(0, 0, 150000, 150000), 
                    CELLS_PER_OBJECT = 16, PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedLocations");

            migrationBuilder.DropTable(
                name: "LocationLink");

            migrationBuilder.DropTable(
                name: "PermittedStructureLink");

            migrationBuilder.DropTable(
                name: "StructureLink");

            migrationBuilder.DropTable(
                name: "StructureSpatialCache");

            migrationBuilder.DropTable(
                name: "StructureTemplates");

            migrationBuilder.DropTable(
                name: "Location");

            migrationBuilder.DropTable(
                name: "Structure");

            migrationBuilder.DropTable(
                name: "StructureType");
        }
    }
}
