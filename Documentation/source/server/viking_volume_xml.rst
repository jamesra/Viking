
####################
The VikingXML format
####################

Viking is capable of displaying any correct formatted image data. 
To use Viking for different images you need to make a VikingXML file available on a web server 
which you can then pass to Viking's splash screen. 

Nornir contains scripts which VikingXML files automatically. 

Link to the `XML Schema Definition for VikingXML_`

Viking has not been tested against a lot of different input and does not yet have robust error reporting. 
We recommend testing after adding each element and not deviating too much from the sample below at first. 
Please let Dr. James Anderson know if you encounter trouble (james.r.anderson at utah.edu).

Sample VikingXML
================

Below is an example `snippet of VikingXML`_ which loads one section of our RC1 volume:: 

   <?xml version="1.0"?>
	<Volume name="Rabbit" UniqueID="1" num_stos="340"  num_sections="341" path="http://155.100.105.9/Rabbit"
	  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="http://connectome.utah.edu/VikingXML.xsd">
		<stos mappedSection="2" controlSection="1" pixelSpacing="16" type="grid" path="0002-0001_grid_16.stos"/>
		<stos mappedSection="3" controlSection="2" pixelSpacing="16" type="grid" path="0003-0002_grid_16.stos"/>
		<Section number="30" path="0030" >
			<transform name="0030_Supertile.mosaic" path="0030_Supertile.mosaic" UseForVolume="false" FilePrefix="0030" FilePostfix=".png" />
			<transform name="grid.mosaic" path="grid.mosaic" UseForVolume="true" FilePrefix="0030" FilePostfix=".png" />
			<transform name="translate.mosaic" path="translate.mosaic" UseForVolume="false" FilePrefix="0030" FilePostfix=".png" />
			<Pyramid name="8-bit" path="8-bit">
				<Level Downsample="1" path="001"/>
				<Level Downsample="2" path="002"/>
				<Level Downsample="4" path="004"/>
				<Level Downsample="8" path="008"/>
				<Level Downsample="16" path="016"/>
				<Level Downsample="32" path="032"/>
				<Level Downsample="64" path="064"/>
			</Pyramid>
			<Tileset name="mosaic" path="mosaic" FilePrefix="0030_" FilePostfix=".png" TileXDim="256" TileYDim="256" >
				<Level Downsample="1" GridDimX="462" GridDimY="466" path="001"/>
				<Level Downsample="2" GridDimX="231" GridDimY="233" path="002"/>
				<Level Downsample="4" GridDimX="116" GridDimY="117" path="004"/>
				<Level Downsample="8" GridDimX="58" GridDimY="59" path="008"/>
				<Level Downsample="16" GridDimX="29" GridDimY="30" path="016"/>
				<Level Downsample="32" GridDimX="15" GridDimY="15" path="032"/>
				<Level Downsample="64" GridDimX="8" GridDimY="8" path="064"/>
			</Tileset>
			<Tileset name="Glycine" path="Glycine" FilePrefix="" FilePostfix=".png"  TileXDim="256" TileYDim="256">
				<Level GridDimX="15" GridDimY="15" Downsample="32" path="032"/>
				<Level GridDimX="8" GridDimY="8" Downsample="64" path="064"/>
			</Tileset>
			<ChannelInfo>
				<Channel Section="Selected" Channel="mosaic" Color="0xFF00FF"/>
				<Channel Section="Selected" Channel="Glycine" Color="0x00FF00"/>
			</ChannelInfo>
		</Section>
	</Volume>

VikingXML Element Documentation
===============================

Volume
------
   Required element which contains all information about the volume and optionally points to the VikigXML schema definition.
   
   ::
   
   <Volume name="Rabbit" UniqueID="1" num_stos="340"  num_sections="341" path="http://155.100.105.9/Rabbit" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="http://connectome.utah.edu/VikingXML.xsd">
   
   :Name: The name of the volume.
   :num_stos: Optional, the number of stos tags in the volume. Only used for loading progress bar and will eventually be removed.
   :num_sections: Optional, the number of section tags in the volume. Only used for loading progress bar and will eventually be removed.
   :path: Required, the URL of the root path of the volume. Only used when VikingXML is loaded from local disk.

stos
----

   Optional element which defines a slice-to-slice transform (stos). These are used to tell Viking how to warp from section space to volume space. All attributes are required.
   
   ::
   
   <stos mappedSection="2" controlSection="1" pixelSpacing="16" type="grid" path="0002-0001_grid_16.stos"/>
   
   :mappedSection: The section being transformed
   :controlSection: The section not being transformed
   :pixelSpacing: stos files are rarely generated against the full-resolution data. This value tells viking how much the coordinates should be scaled by to match the actual image dimensions.
   :type: The type of transform to use, currently always use "grid".
   :path: Relative path from Volume path to the stos file containing transformation data.

Section
-------

   The section tag describes a slice in the volume at a specific z depth. If there are no section tags there are no images in the volume.
   
   ::

   <Section number="30" path="0030" >

   :name: Friendly name of the section. Defaults to section number. I recommend using section number because I haven't tested anything else.
   :number: Integer, numbers should be sequential according to the order in which sections were cut. It is OK to skip numbers for lost sections.
   :path: Relative path from Volume path to the Section directory.
 
Pyramid
-------

   Image pyramids are generated from the original images captured by an imaging platform.
   
   ::
   
   <Pyramid name="8-bit" path="8-bit">
 
   We use these image pyramids with the NCRToolset to generate transforms that describe 
   where each tile in the pyramid is positioned in section space. In the pyramid each tile size 
   is variable according to its source pyramid level. This makes for poor performance over the internet 
   but is very useful for debugging the output of the NCRToolset over an intranet.

   :name: Name of the channel displayed in the Viking UI.
   :path: Relative path from the section path.
   
Transform
---------
   
   Transforms are only applied to Pyramids. They are generated by the NCRToolset, using the ITK string formatting for transforms, and describe how each tile in a mosaic is positioned in the section.
   
   ::
     
   <transform name="grid.mosaic" path="grid.mosaic" UseForVolume="true" FilePrefix="0030" FilePostfix=".png" />
   
   :Name: Name of the transform in the Viking UI
   :path: Relative path from the section path.
   :UseForVolume: Boolean, specifies that this transform is used to position tiles in the section before the are warped into the volume. Only the highest quality transform should have this set.
   :FilePrefix: String to prepend to all file names, if needed. A period is added after the file prefix. Traditionally we prepend the section number to tile names. Tiles are expected to be numbered with three digits. i.e. Section 1 Tile 243 = 0001.243.png
   :FilePostfix: Extension to add to file names, must be supported by XNA library. .png format recommended.
   
Tileset
-------

   In a tileset all the images have a fixed size regardless of the level of the pyramid. 
   We typically use 256x256 pixel tiles laid out on a grid with no overlap. This optimizes 
   bandwidth use. Tile names include the grid position, i.e. X001_Y001.png
   
   ::
   
   <Tileset name="Glycine" path="Glycine" FilePrefix="" FilePostfix=".png"  TileXDim="256" TileYDim="256">

   :Name: Name of the channel displayed in the Viking UI
   :path: Relative path from the section path.
   :UseForVolume: Boolean, specifies that this transform is used to position tiles in the section before the are warped into the volume. Only the highest quality transform should have this set.
   :FilePrefix: String to prepend to all file names, if needed. A period is added after the file prefix. Traditionally we prepend the section number to tile names. Tiles are expected to be numbered with three digits. i.e. Section 1 Tile 243 = 0001.243.png
   :FilePostfix: Extension to add to file names, must be supported by XNA library. .png format recommended.
   :TileXDim: Pixel X dimensions of each tile
   :TileYDim: Pixel Y dimensions of each tile

Level
-----

   Describes a level in a "tileset" or "pyramid" image pyramid. A level is a directory containing all of the original tiles downsampled by a common factor.
   
   ::
   
   <Level Downsample="1" GridDimX="462" GridDimY="466" path="001"/>

   :Level: A number defining what level of the pyramid this is. Must currently be a power of two.
   :path: Relative path from the tileset or pyramid path.
   :GridDimX: Tilesets only, Integer defining the dimensions of the grid in X
   :GridDimY: Tilesets only, Integer defining the dimensions of the grid in Y
   
   
ChannelInfo
-----------
   
   Channel info is an optional element allowing volumes to define a default mix of channels for a section. 
   This element can also be placed under the volume element to define a global default channel setup. 
   Section channel configurations override volume channel configurations.

   ::
   
   <ChannelInfo>
   

Channel
-------

   Specifies a channel to display by default.
   
   ::
   
   <Channel Section="Selected" Channel="Glycine" Color="0x00FF00"/>
   
   
   :Section: The section to load the channel from, must be one of these values: Defines which section to load a channel from
      * **Selected**: Load images from the users currently selected section
      * **Fixed**: Load images from the specified section
      * **Above**: Load images from the reference section above the users selected section
      * *Below**: Load images from the reference section below the users selected section
   :Channel: The name of the channel, either a pyramid or tileset, to load, or "Selected" for user selected channel
   :Color: Color to use when displaying channel, specify as a web color i.e. #00ff00

.. _snippet of VikingXML: http://connectomes.utah.edu/Rabbit/Volume.VikingXML
.. _XML Schema Definition for VikingXML: https://github.com/jamesra/Viking/VikingXML.xsd