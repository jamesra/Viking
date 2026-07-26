# Spatial Columns Fact Sheet

A one-page reference for citing when SQL Server spatial geometry was added to the
Viking annotation database. Prepared for the 3D connectome analysis methods paper.

## When were spatial columns added?

The `MosaicShape` and `VolumeShape` spatial `geometry` columns were added to the
`Location` table on **19 August 2015**, in commit `ffe9cceb` on the `dev` branch
("Migration to spatial columns in database", author James Anderson). That is
roughly **11 years** before the present (June 2026) and about **6 years after** the
2009 PLoS Biology framework paper (Anderson et al.).

The change is recorded in the versioned database build script,
[`Servers/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql`](../../../Servers/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql)
(originally `Servers/AnnotationService/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql`
before later repository reorganization).

## What changed (two-step migration)

The migration is applied as two sequential schema versions in the `DBVersion`
table:

| DBVersion | Date | Change |
|-----------|------|--------|
| 24 | 2015-08-19 | `ALTER TABLE Location ADD MosaicShape geometry` and `ADD VolumeShape geometry` |
| 25 | 2015-08-19 | Populate the new shapes from the legacy scalar columns; drop `Verticies`, `X`, `Y`, `VolumeX`, `VolumeY`; re-add `X`/`Y`/`VolumeX`/`VolumeY` as persisted computed centroids of the shapes |

Before this migration, an annotation footprint was stored as scalar columns
(`X`, `Y`, `Radius`) plus a `Verticies` vertex list for polygons. After the
migration, each `Location` row stores two true geometries:

- `MosaicShape` -- the footprint in the native section/mosaic coordinate space.
- `VolumeShape` -- the same footprint in the registered volume coordinate space.

Circular annotations were converted to `CURVEPOLYGON(CIRCULARSTRING(...))` and
point annotations to `POINT(...)`, as seen in the version 25 block of the build
script (around lines 2389-2440). The scalar centroid columns are retained as
`PERSISTED` computed columns, for example:

```sql
ALTER TABLE Location ADD X as ISNULL(MosaicShape.STCentroid().STX, ISNULL(MosaicShape.STX,0)) PERSISTED
```

## Follow-up spatial commits (same era)

| Commit | Date | Summary |
|--------|------|---------|
| `ffe9cceb` | 2015-08-19 | Migration to spatial columns in database (DBVersion 24-25) |
| `b579196a` | 2015-08-21 | Further updates for spatial migration |
| `19b70b34` | 2015-08-26 | Add spatial index to the database |
| `64203551` | 2016-03-24 | Spatial queries use mosaic or volume coordinates; synapse-to-lines migration |

The same script has since grown to **DBVersion 83**, which improves the spatial
bounds stored procedure `SelectSectionLocationsAndLinksInMosaicBounds`.

## Methods-ready paragraph (copy/paste)

> Annotation geometry in the Viking database was migrated from scalar
> coordinates (X, Y, radius, and polygon vertex lists) to native SQL Server
> `geometry` types in August 2015 (database schema versions 24 and 25). Each
> annotation footprint (`Location`) stores a `MosaicShape` and a `VolumeShape`,
> representing the footprint in section-mosaic and registered-volume coordinate
> spaces respectively; the scalar centroid columns (X, Y, VolumeX, VolumeY) are
> retained as persisted computed columns derived from those geometries. Spatial
> indexes (added August 2015) and region/bounds stored procedures support
> efficient viewport and radius queries during connectome tracing, review, and
> 3D reconstruction.

## How to reproduce these dates

```bash
# Primary introduction of the spatial columns
git log origin/dev --format="%h %ad %an %s" --date=short \
  -S "MosaicShape geometry" -- "**/CreateUpdateDatabase.sql"

# Inspect the follow-up commits
git show -s --format="%h %ad %an %s" --date=short b579196a 19b70b34 64203551
```
