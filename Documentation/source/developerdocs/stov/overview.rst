##################################################
Slice to Volume Transformation Overview
##################################################

Viking users typically annotate in 'Volume' space, where all sections have been registered into a single 3D space.  However Viking stores these annotations in Section (a.k.a. 'Mosaic') space.  This often causes more sophisticated users consternation as there is no easy way to retrieve images the annotations describe.

Glossary
--------

    :mosaic: The layout of individual captures to create a single image of a section. 
     
    :.mosaic: A file containing a transform for each individual image in a capture that position them in a shared 2D section space.  The ideal result is a single seemless image of the entire captured section.
    
    :tile/image: A single image captured during section acquisition.  A single section's mosaic is composed of many, often thousands, of individual tiles.  A tile is typically about 4096x4096 pixels and too large for good client performance.
    
    :optimized tile: After mosaics are generated optimized tiles can be generated in either mosaic or volume space.  These are 256x256 or 512x512 non-overlapping tiles at full and downsampled resolutions.  These images are used by Viking for most public volumes for performance reasons.  However generating these images can take weeks or months for a volume.
    
    :slice/section: Used interchangeably.  A single section cut from a plastic block and placed on either a gold grid for transmission electron microscopy (TEM) capture or a glass slide for light microscopy (LM) capture.
    
    :slice-to-slice (stos): A transformation registering one section to another section.
    
    :slice-to-volume (stov): A transformation registering one section into the volume space.
    
    :.stos: A file containing a single transformation to register one image onto another.  Used to register a slice onto either an adjacent slice or volume space.


Background
----------

The primary reason Viking stores annotations in mosaic space is to facilitate re-registering the slice-to-volume transformations frequently without requiring any update to the annotation database.  Instead the client applies the slice-to-volume transforms at runtime.  Storing annotations in section space also ensures 2D measurements requested from the server-side database are free of additional distortion that may be introduced with another slice-to-volume transformation applied to the section.

On the server side the practice allows one to optimize a set of tiles for each mosaic and then have Viking apply tranformations using the graphics processing unit (GPU) on the client computer to produce the latest volume registered images.  

The result is slice-to-slice and slice-to-volume updates do not require changes to either annotations or images.  Thus minor tweaks to improve registration quality require minimal I/O and impact to both the server and the clients.

The downside to this approach is Viking, in particular our first volume RC1, does not have a trivial way to share stacks of volume registered images in 3D for annotations. 

Exporting images for annotations using tilesets
-----------------------------------------------

For volumes after RC1 it would be possible to export full resolution registered images of each section using Nornir.  However if the slice-to-slice transforms are updated these images will no longer correlate to the annotations.

For RC1 the transforms have been largely stable for years.  There is a set of optimized tiles exported in volume space.  

If volume registered images are not required then the optimized tiles for the section can be used to recreate the image under the annotation.

The Tileset meta-data
=====================

The .VikingXML file used to load a volume contains the information required to create a URL to load any image in the volume.

* Example tileset from a .VikingXML file.*::
 
    <Tileset FilePostfix=".png" FilePrefix="0234_" TileXDim="256" TileYDim="256" name="TEM" path="TEM">
        <Level Downsample="1" GridDimX="461" GridDimY="463" path="001"/>
        <Level Downsample="2" GridDimX="231" GridDimY="232" path="002"/>
        <Level Downsample="4" GridDimX="116" GridDimY="116" path="004"/>
        <Level Downsample="8" GridDimX="58" GridDimY="58" path="008"/>
        <Level Downsample="16" GridDimX="29" GridDimY="29" path="016"/>
        <Level Downsample="32" GridDimX="15" GridDimY="15" path="032"/>
        <Level Downsample="64" GridDimX="8" GridDimY="8" path="064"/>
        <Level Downsample="128" GridDimX="4" GridDimY="4" path="128"/>
        <Level Downsample="256" GridDimX="2" GridDimY="2" path="256"/>
        <Level Downsample="463" GridDimX="1" GridDimY="1" path="463"/>
    </Tileset>

Tileset attributes:
    
    :TileXDim, TileYDim: Dimensions of each tile in pixels
    :name: Name of the tileset
    :path: Path to the tileset, append to path elements from the root to create the URL required to reach an image
    :FilePostfix, FilePrefix: String to prepend/append to the tile filename.
    
Level attributes:
  
    :Downsample: The factor, as a power of 2, that the images have been downsampled by.  All images have the same pixel dimensions so the grid dimensions will decrease by a factor of 2 for every increase in downsample level.
    :GridDimX, GridDimY: The number of tiles in the X/Y axis for this downsample level
    :path: path to the tileset level, append to path elements from the XML root to this element to construct a URL.
    
Loading an image for a coordinate
=================================

Lets say we want the full resolution image for a point at 23456x, 54321y from section #234 of RC1.  We begin by opening the VikingXML for RC1 and finding the <Level Downsample="1"> element of section 234.  We then walk to the root of the XML document to the Level element appending the 'path' attributes::

    http://storage1.connectomes.utah.edu/RABBIT/
        0234
            TEM
                001 

Thus the folder containing the images we desire is::

    http://storage1.connectomes.utah.edu/RABBIT/0234/TEM/001/

Next calculate which tile to load::  

    GridX = 23456 / Level.TileXDim = 23456 / 256 = 91.625
    GridY = 54321 / Level.TileYDim = 54321 / 256 = 212.191
    
Taking the floor of the provides the grid coordinates and the fractional remainder indicates where in the image our point lies::

    X = math.floor(91.625) = 91
    Y = math.floor(212.191) = 212
    
    XPixelOffset = 0.625 * Level.TileXDim = 160
    YPixelOffset = 0.191 * Level.TileYDim = 49
    
We can then construct the filename using the <Tileset> metadata.  Note the coordinates should be zero padded to three digits or more::

    $"{Tileset.TilePrefix}_X{X:03d}_Y{Y:03d}{Tilset.TilePostfix}"
    http://storage1.connectomes.utah.edu/RABBIT/0234/TEM/001/0234_X160_Y049.png

Plugging the resulting url into your browser should load the tile of interest.  The tiles do not overlap, so if a larger area is required one can add the adjacent tiles to build the image. 

Volume registered tiles for the RC1 volume
==========================================

Viking by default only generates optimized tiles in mosaic space.  For RC1 there exists volume registered tiles exported from the Viking Client.  The URL for a folder is slightly modified from the above instructions.  (Note in particular that the section folder is padded with three digits instead of four)::

    http://storage1.connectomes.utah.edu/RC1VolumeRegisteredV2/RC1/000/Tiles/001/
    
Using that URL with the instructions above we can load the sample coordinates for point at 23456x, 54321y from section #234 in volume registered space with this URL::

 http://storage1.connectomes.utah.edu/RC1VolumeRegisteredV2/RC1/234/Tiles/001/X160_Y049.png
    
Additionally, for this RC1 resource only each folder has .xml meta-data located in an xml file based on the four digit section number::

    http://storage1.connectomes.utah.edu/RC1VolumeRegisteredV2/RC1/234/Tiles/001/0234.xml
    

Volume registered images for other volumes
==========================================

The easiest way to conduct mapping for later volumes would be to export full resolution images from the volumes and provide them. 




    
    
    
 


 


