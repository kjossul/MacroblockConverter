using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;
using System.IO;

public class Converter
{
    private readonly List<string> sourceFiles;
    private readonly bool preserveTrimmed;
    private readonly bool nullifyVariants;
    private readonly bool createConvertedFolder;
    private readonly Action<string> log;
    private readonly string[] excludedPrefixes = 
    [
        "SnowRoad", "RallyCastle", "RallyRoad", "TrackWall", "DecoWall", "StructureStraightInTrackWall", "Canopy",
        "RoadWater", "PlatformWater", "Water", "Stage", "DecoHill", "DecoCliff"
    ];
    private readonly string[] allowedPrefixes =
    [
        "DecoWallBase", "DecoWallSlope2Straight", "DecoWallDiag1"
    ];
    private readonly string[] excludedSuffixes =
    [
        "TurboRoulette"      
    ];
    
    private readonly Dictionary<string, int> environments = new()
    {
        { "BlueBay", 28 },
        { "GreenCoast", 15 },
        { "RedIsland", 16 },
        { "WhiteShore", 29 },
    };

    public Converter(List<string> sourceFiles, bool preserveTrimmed, bool nullifyVariants, bool createConvertedFolder, Action<string> log)
    {
        this.sourceFiles = sourceFiles;
        this.preserveTrimmed = preserveTrimmed;
        this.nullifyVariants = nullifyVariants;
        this.createConvertedFolder = createConvertedFolder;
        this.log = log;
        
        Gbx.LZO = new Lzo();
    }

    public void Convert()
    {
        int totalSkipped = 0;

        foreach (var sourceFile in sourceFiles)
        {
            string baseBlocksPath = GetBaseBlocksPath(sourceFile);
            string relativePath = GetRelativePath(baseBlocksPath, sourceFile);
            try
            {
                var macroBlock = Gbx.ParseNode<CGameCtnMacroBlockInfo>(sourceFile);
                macroBlock.AutoTerrains = [];
                var validBlocks = CollectValidBlocks(macroBlock.BlockSpawns);

                if (validBlocks.Count == 0 && macroBlock.ObjectSpawns.Count == 0)
                {
                    log($"{sourceFile} Skipped: contains no convertible Macroblocks or items.");
                    totalSkipped++;
                    continue;
                }          

                if (!preserveTrimmed && validBlocks.Count < macroBlock.BlockSpawns.Count)
                {
                    log($"{sourceFile} Skipped: Macroblocks has some invalid blocks.");
                    totalSkipped++;
                    continue;
                }
                foreach (var env in environments)
                {
                    string envName = env.Key;
                    int collectionId = env.Value;
                    if (macroBlock.Ident.Collection.Number == collectionId) { continue; }
                    string destFolder = Path.Combine(baseBlocksPath, envName, createConvertedFolder ? "Converted" : "");
                    string destPath = Path.Combine(destFolder, relativePath);
                    string destDirectory = Path.GetDirectoryName(destPath);

                    if (!Directory.Exists(destDirectory))
                    {
                        Directory.CreateDirectory(destDirectory);
                    }

                    foreach (var block in validBlocks)
                    {
                        block.BlockModel = new Ident(
                            block.BlockModel.Id,
                            collectionId,
                            block.BlockModel.Author
                        );
                    }
                    macroBlock.Ident = new Ident(
                        macroBlock.Ident.Id,
                        collectionId,
                        macroBlock.Ident.Author
                    );
                    macroBlock.BlockSpawns = validBlocks;
                    macroBlock.Save(destPath);
                }
            }
            catch (Exception ex)
            {
                log($"Error processing file {sourceFile}: {ex.Message}");
                totalSkipped++;
            }
        }

        log($"=== Summary ===");
        log($"Converted: {sourceFiles.Count - totalSkipped}");
        log($"Skipped: {totalSkipped}");
    }

    private List<CGameCtnMacroBlockInfo.BlockSpawn> CollectValidBlocks(IList<CGameCtnMacroBlockInfo.BlockSpawn> blockSpawns)
    {
        var validBlocks = new List<CGameCtnMacroBlockInfo.BlockSpawn>();

        foreach (var block in blockSpawns)
        {
            if (block.BlockModel == null)
                continue;

            if (block.BlockModel.Author != "Nadeo")
                continue;

            string blockName = block.BlockModel.Id;
            bool isValid = true;

            foreach (var prefix in excludedPrefixes)
            {
                if (blockName.StartsWith(prefix))
                {
                    isValid = false;
                    break;
                }
            }

            foreach (var prefix in allowedPrefixes)
            {
                if (blockName.StartsWith(prefix))
                {
                    isValid = true;
                    break;
                }
            }

            foreach (var suffix in excludedSuffixes)
            {
                if (blockName.EndsWith(suffix))
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid)
            {
                // perform name conversions
                // TODO convert on the opposite direction for stadium
                if (blockName.Equals("DecoWallBase"))
                {
                    blockName = blockName.Replace("DecoWallBase", "DecoWallBasePillar");
                }
                else if (blockName.StartsWith("DecoPlatformDirt"))
                {
                    blockName = blockName.Replace("Dirt", "");
                }
                else if (blockName.StartsWith("DecoPlatformIce"))
                {
                    blockName = blockName.Replace("Ice", "");
                }
                block.BlockModel = new Ident(blockName, block.BlockModel.Collection, block.BlockModel.Author);
                // nullify variants
                if (nullifyVariants)
                {
                    block.Flags = (int)((uint)block.Flags & 0xFF000000u);
                }
                validBlocks.Add(block);
            }
        }

        return validBlocks;
    }

    private string GetBaseBlocksPath(string sourceFile)
    {
        string[] parts = sourceFile.Split(Path.DirectorySeparatorChar);
        
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("Blocks", StringComparison.OrdinalIgnoreCase))
            {
                // Reconstruct path up to and including "Blocks"
                return string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i + 1));
            }
        }
        
        // Fallback: use Documents/Trackmania/Blocks
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, @"Trackmania\Blocks");
    }
    private string GetRelativePath(string basePath, string sourceFile)
    {
        string afterBlocks = Path.GetRelativePath(basePath, sourceFile);
        
        // Remove the first folder (Stadium or whatever environment it's in)
        string[] parts = afterBlocks.Split(Path.DirectorySeparatorChar);
        if (parts.Length > 1)
        {
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(1));
        }
        
        return Path.GetFileName(sourceFile);
    }
}