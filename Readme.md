# MacroBlock Converter

This app allows you to convert macroblocks between the five TM2020 collections (Stadium, Red Island, Green Coast, Blue Bay, White Shore).

![Demo](Screenshots/demo.png)
![Screenshot](Screenshots/gui.png)

## Install guide

Go to Releases, download the zip file, extract it and run the executable.

## Usage

On the left, you'll find two buttons that let you choose your macroblocks to convert. You can either choose each file individually, or you can select a folder and the program will look recursively for all macroblocks in the path.

Once the macroblock(s) are selected, the `Convert` button will become available. Click it, and you'll find the converted macroblocks into `<Trackmania_Documents>/Blocks/<Target_Collection>/<Source_Collection>Converted/`, preserving the original filepath structure.

## Options guide
- `Preserve trimmed macroblocks`: if this is checked, macroblocks with invalid blocks will still be converted, but the invalid blocks will be removed. If this is unchecked, the macroblock gets skipped completely.
- `Set base variants`: convert blocks by only preserving the placement mode, and setting variants to 0. **You most likely want to keep this checked**, because the game will crash if you try to open the editor when you have a macroblock with invalid block variants. Currently, I'm just preserving the variant of `StructureSupport` and `StageTechnicsLight`, which I know can be converted safely. If you know of other blocks that have all variants shared across all collections, please let me know.
- `Convert Ground mode to Air`: since Vistas have terrain, you can have macroblocks with blocks in ground mode at different heights. The game will not let you place a macroblock if it cannot find a way to have terrain under the ground blocks, so with this option you can make sure to bypass this restriction.
- `Ignore Vegetation`: with this checked, all trees and vegetation will be removed from the macroblock. Useful if you're macroblocking a full map, and don't want vegetation floating around in the converted macroblock.

### Converting blocks to items
Vistas have a severely trimmed set of blocks compared to Stadium. This option will allow you to convert blocks into items. Beware that items get embedded into the map, and there is a hard 5MB item limit for maps to be playable online, so keep a check on the size of embedded items.

At first launch, you'll be missing the items required for this option to work. Just click the download button and wait patiently for `items.zip` to be downloaded and extracted. They will be extracted to `Items/0-B-NoUpload/MacroblockConverter`. This path avoids conflict with the widely used [TrackmaniaItemsSorted](https://github.com/ski-freak/TrackmaniaItemsSorted) items collection made by SkiFreak.

> [!IMPORTANT]
> I'm using a lot of items that have been converted automatically from blocks inside the MeshModeller. These have a bloated size and bad lightmap. If you want to contribute, please consider making a pull request with better items.

To reduce the amount of embedded items, the program allows you to pick and choose which collection of blocks to convert. This is the currently available list, and the origin of the item set:

| Set Name | Source | Notes |
|---|---|---|
| TrackWall | MeshModeller | [Link](https://github.com/RuurdBijlsma/tm-convert-blocks-to-items/releases/tag/07-04-22) |
| DecoWall | MeshModeller | [Link](https://github.com/RuurdBijlsma/tm-convert-blocks-to-items/releases/tag/07-04-22) |
| DecoHill | MeshModeller | [Link](https://github.com/RuurdBijlsma/tm-convert-blocks-to-items/releases/tag/07-04-22) |
| HillsShort | MeshModeller | [Link](https://github.com/RuurdBijlsma/tm-convert-blocks-to-items/releases/tag/07-04-22). Some textures are weird. |
| SnowRoad | [Vista Wood by Yin_TM](https://item.exchange/set/view/13284) |  |
| RallyCastle | [Vista Castle by Yin_TM](https://item.exchange/set/view/13285) | Clips need to be placed manually |
| RallyRoad | [Vista Rally by Yin_TM](https://item.exchange/set/view/13297) | Clips need to be placed manually |
| Transitions | [Vista Transitions by Yin_TM](https://item.exchange/set/view/13290) | Road/Platform to TrackWall/DecoWall |
| Stage | MeshModeller | Manually made by me |
| Canopy | MeshModeller | Manually made by me |
## For developers

I'm not an experienced C# dev, nor GBX expert. Any PR is welcome, as I'm probably missing many details.

The current implementation simply checks for allowed suffixes/prefixes in the block name, and replaces the `Ident` field in the macroblock and on each block with the correct one for each vista. Furthermore, it clears the `AutoTerrain` array, as in my experience it caused crashing.

## Thanks to

Special thanks to:
- BigBang1112 for the amazing work on [GBX.NET](https://github.com/BigBang1112/gbx-net)
- Zai for letting me know about the issue with block variants and giving me tips.
