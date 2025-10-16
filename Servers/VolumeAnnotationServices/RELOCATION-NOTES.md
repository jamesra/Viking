# Relocation Notes - Volume Annotation Services

## Summary

The Docker configuration for Viking's volume annotation services has been organized into a dedicated directory under `Servers/VolumeAnnotationServices/`.

## What Changed

### Directory Structure

**Before**:
```
VikingLegacy/
├── Dockerfile.Combined          # At solution root
└── ... other files
```

**After**:
```
VikingLegacy/
├── Servers/
│   └── VolumeAnnotationServices/
│       ├── Dockerfile            # Relocated and enhanced
│       ├── README.md             # Service documentation
│       └── RELOCATION-NOTES.md   # This file
└── ... other files
```

### File Changes

| File | Change |
|------|--------|
| `Dockerfile.Combined` | Moved to `Servers/VolumeAnnotationServices/Dockerfile` |
| N/A | Created `Servers/VolumeAnnotationServices/README.md` |
| N/A | Created `Servers/VolumeAnnotationServices/RELOCATION-NOTES.md` |

### Service Naming

The services have been renamed to better reflect their purpose:

| Old Name | New Name |
|----------|----------|
| `viking-combined-services` | `viking-annotation-services` |
| `viking-services` (container) | `viking-annotation-services` |

### Updated References

All documentation and scripts have been updated to reference the new location:

- ✅ `docker-compose.combined.yml` - Updated dockerfile path
- ✅ `Scripts/BuildAndRunCombined.ps1` - Updated build path and image names
- ✅ `Scripts/ValidateDockerSetup.ps1` - Updated validation checks
- ✅ `Scripts/TestDockerImage.ps1` - Updated container name
- ✅ `DOCKER-COMBINED-QUICKSTART.md` - Updated all references
- ✅ `Docker-Combined-Services-README.md` - Updated paths and examples
- ✅ `COMBINED-SERVICES-SUMMARY.md` - Updated file locations
- ✅ `DOCKER-BUILD-FIXES.md` - Updated container names

## Why This Change?

### Better Organization

1. **Logical Grouping**: Docker configuration now lives with the services it deploys
2. **Clear Purpose**: Directory name explicitly indicates these are volume annotation services
3. **Scalability**: Makes it easier to add additional Docker configurations for other service groups

### Improved Documentation

1. **Service-Specific Documentation**: `README.md` in the VolumeAnnotationServices directory provides focused documentation
2. **Clearer Context**: The Dockerfile header now clearly states it's for volume annotation services
3. **Better Discoverability**: Developers looking at server projects can immediately see Docker options

### Professional Structure

1. **Industry Standard**: Multi-service Docker configs typically live under a dedicated directory
2. **Separation of Concerns**: Server-related Docker files are now grouped with server projects
3. **Easier Maintenance**: All volume annotation service artifacts are in one location

## Service Description

The relocated Dockerfile creates a container with **three interconnected services for volume annotation**:

### 1. AnnotationService (`/annotation`)
- **Purpose**: Volume annotation CRUD operations
- **Type**: WCF Service (.NET Framework 4.8)
- **Responsibilities**:
  - Structure creation, modification, deletion
  - Location management
  - Structure type administration
  - Permitted structure link definitions
  - Volume metadata access

### 2. ConnectomeODataV4 (`/odata`)
- **Purpose**: Queryable access to connectome data
- **Type**: OData v4 Web API (.NET Framework 4.8)
- **Responsibilities**:
  - RESTful data queries
  - Spatial data access
  - Structure relationship queries
  - Location queries with filtering
  - OData standard compliance ($filter, $select, $expand, etc.)

### 3. DataExport (`/dataexport`)
- **Purpose**: Export morphology and network data
- **Type**: ASP.NET Core API (.NET 9.0)
- **Responsibilities**:
  - Morphology export (SWC, Collada, OBJ formats)
  - Network/circuit export
  - Graph data generation
  - Motif analysis exports
  - High-performance bulk data retrieval

## Migration Guide

### If You Have Existing Scripts

If you have custom scripts referencing the old location:

**Old Reference**:
```powershell
docker build -f Dockerfile.Combined -t viking-combined-services:latest .
```

**New Reference**:
```powershell
docker build -f Servers/VolumeAnnotationServices/Dockerfile -t viking-annotation-services:latest .
```

### If You Use Docker Compose

The `docker-compose.combined.yml` file has been automatically updated. No changes needed.

### If You Use the Automation Scripts

The automation scripts have been automatically updated. Continue using them as before:

```powershell
.\Scripts\BuildAndRunCombined.ps1
```

## No Breaking Changes

This is a **non-breaking change** for users who rely on the automation scripts. The scripts have been updated to use the new locations transparently.

**What Still Works**:
- ✅ `.\Scripts\BuildAndRunCombined.ps1` - All actions work as before
- ✅ `docker-compose -f docker-compose.combined.yml up` - Works with new location
- ✅ Service URLs - Unchanged (still at `/annotation`, `/odata`, `/dataexport`)
- ✅ Port mappings - Unchanged (8080:80, 8443:443)
- ✅ Container functionality - Identical behavior

**What Changed (User-Visible)**:
- 🔄 Container name: `viking-services` → `viking-annotation-services` (configurable)
- 🔄 Image name: `viking-combined-services` → `viking-annotation-services`
- 🔄 Dockerfile location: Root → `Servers/VolumeAnnotationServices/`

## Additional Documentation

For more information, see:

- **`README.md`** (in this directory) - Comprehensive service documentation
- **`../../DOCKER-COMBINED-QUICKSTART.md`** - Quick start guide
- **`../../Docker-Combined-Services-README.md`** - Detailed usage guide
- **`../../COMBINED-SERVICES-SUMMARY.md`** - Implementation summary
- **`../../DOCKER-BUILD-FIXES.md`** - Build troubleshooting

## Questions?

If you have questions about this relocation:

1. Check the `README.md` in this directory
2. Review the updated quick start guide
3. Run validation: `.\Scripts\ValidateDockerSetup.ps1`
4. Test your setup: `.\Scripts\TestDockerImage.ps1`

---

**Date of Relocation**: October 14, 2025  
**Previous Location**: `Dockerfile.Combined` (solution root)  
**New Location**: `Servers/VolumeAnnotationServices/Dockerfile`  
**Purpose**: Better organization and clearer service categorization


