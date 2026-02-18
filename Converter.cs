using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.Engines.GameData;
using GBX.NET.LZO;
using System.IO;
using System.Text.Json;

public class Converter
{
    private readonly List<string> sourceFiles;
    private readonly bool preserveTrimmed;
    private readonly bool nullifyVariants;
    private readonly bool createConvertedFolder;
    private readonly bool convertBlocksToItems;
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
        { "Stadium", 26 }
    };

    public Converter(List<string> sourceFiles, bool preserveTrimmed, bool nullifyVariants, bool createConvertedFolder, bool convertBlocksToItems, Action<string> log)
    {
        this.sourceFiles = sourceFiles;
        this.preserveTrimmed = preserveTrimmed;
        this.nullifyVariants = nullifyVariants;
        this.createConvertedFolder = createConvertedFolder;
        this.convertBlocksToItems = convertBlocksToItems;
        this.log = log;

        Gbx.LZO = new Lzo();
    }

    public void Convert()
    {
        int totalSkipped = 0;

        string blocksListFilePath = AppDomain.CurrentDomain.BaseDirectory + @"conversions.json";
        Dictionary<string, string> blockToItem = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(blocksListFilePath));

        foreach (var sourceFile in sourceFiles)
        {
            // find base blocks directory and relative path (might be in Trackmania2020 instead of Trackmania if double install with TMNF)
            string[] parts = Path.GetFullPath(sourceFile).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            string baseBlocksPath = null;
            string relativePath = null;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("Blocks", StringComparison.OrdinalIgnoreCase))
                {
                    baseBlocksPath = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i + 1));
                    relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(i + 2));
                    break;
                }
            }
            if (baseBlocksPath == null)
            {
                log("Didn't find 'Blocks' directory in block path, please work with the standard game filesystem.");
                return;
            }
            try
            {
                var macroBlock = Gbx.ParseNode<CGameCtnMacroBlockInfo>(sourceFile);
                var sourceEnv = macroBlock.Ident.Collection.Number;
                macroBlock.AutoTerrains = [];
                var validBlocks = CollectValidBlocks(macroBlock.BlockSpawns);
                if (convertBlocksToItems) { 
                    macroBlock.ObjectSpawns.AddRange(CollectConvertibleBlocks(macroBlock.BlockSpawns, blockToItem));
                }

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
                    if (sourceEnv == collectionId) { continue; }
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

    private List<CGameCtnMacroBlockInfo.ObjectSpawn> CollectConvertibleBlocks(List<CGameCtnMacroBlockInfo.BlockSpawn> blockSpawns, Dictionary<string, string>? blockToItem)
    {
        var objectSpawns = new List<CGameCtnMacroBlockInfo.ObjectSpawn>();
        foreach (var block in blockSpawns)
        {
            // check if there is a corresponding item, and this block is not placed in freeblock mode
            if (blockToItem.ContainsKey(block.BlockModel.Id) && (block.Flags >> 24) < 2)
            {
                var objectSpawn = new CGameCtnMacroBlockInfo.ObjectSpawn();
                objectSpawn.ItemModel = new Ident(blockToItem[block.BlockModel.Id], 26, "DSCukfohR1m0kA6A_8pJ9w");
                objectSpawn.PivotPosition = new Vec3(-16, 0, -16);
                var pitch = block.Direction switch
                {
                    Direction.North => 0,
                    Direction.East => -Math.PI / 2,
                    Direction.South => -Math.PI,
                    Direction.West => Math.PI/2,
                };
                objectSpawn.PitchYawRoll = new Vec3((float) pitch, 0, 0);
                objectSpawn.BlockCoord = block.Coord;
                objectSpawn.AbsolutePositionInMap = block.Coord * (32,8,32) - objectSpawn.PivotPosition;
                objectSpawn.Scale = 1;
                objectSpawn.Version = 14;
                objectSpawn.U03 = 1;
                objectSpawn.U04 = new Int3(-1, -1, -1);
                objectSpawn.U10 = -1;
                objectSpawns.Add(objectSpawn);
            }
        }
        return objectSpawns;
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
                if (nullifyVariants)
                {
                    block.Flags = (int)((uint)block.Flags & 0xFF000000u);  // preserve only placement mode
                }
                validBlocks.Add(block);
            }
        }

        return validBlocks;
    }
}