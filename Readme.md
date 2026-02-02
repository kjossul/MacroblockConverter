# MacroBlock Converter

This app batch converts macroblocks into valid ones for the four new Vistas (Red Island, Green Coast, Blue Bay, White Shore). Note that many blocks are not available in the new Vistas, so they will be skipped during conversion.

## Warnings

Your game will crash if you open the editor with an invalid Macroblock. I had success converting simple routes, but had trouble converting successfully more complicated macroblocks (for instance, the Ascki scenery set). I still don't know which blocks are causing the crashes.

I suggest starting by converting a smaller set of macroblocks, so that you can identify the faulty ones more easily.

## Install guide

Go to Releases, download the zip file and run the executable.

## Usage

You'll be prompted to select a folder. The program will convert all macroblocks found in the given folder and subfolders, and place them in the corresponding directory for the new Vistas.

You can specify two options:
- Whether to create or not the "Converted" top level folder
- Whether to preserve or not trimmed blocks. If this is checked, macroblocks with invalid blocks will still be converted, but the invalid blocks will be removed. If this is unchecked, the macroblock gets skipped completely.

## For developers

I'm not an experienced C# dev, nor GBX expert. Any PR is welcome, as I'm probably missing many details.

The current implementation simply checks for allowed suffixes/prefixes in the block name, and replaces the `Ident` field in the macroblock and on each block with the correct one for each vista. Furthermore, it clears the `AutoTerrain` array, as in my experienced it caused crashing.