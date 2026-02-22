using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.Engines.GameData;
using GBX.NET.LZO;
using System.Formats.Asn1;
using System.IO;
using System.Text.Json;

public class Converter
{
    // blocks that contain these identifiers are filtered out during conversion
    private readonly string[] banList = [
        "Snow", "Rally", "Wall", "Canopy", "Water", "Stage", "Hill", "Cliff", "ToTheme", "Roulette",  // stadium
        "Lake", "River", "Terrain", "Land", "Beach", "Sea", "Shore"  // vistas
    ];

    private readonly string[] whiteList = ["DecoWallBase", "DecoWallSlope2Straight", "DecoWallDiag1"];

    private readonly Dictionary<string, int> environments = new()
    {
        { "BlueBay", 28 },
        { "GreenCoast", 15 },
        { "RedIsland", 16 },
        { "WhiteShore", 29 },
        { "Stadium", 26 }
    };

    private Dictionary<string, Dictionary<string, string>> conversions = new();
    private Dictionary<string, (string, Vec3)> itemInfo = new();  // (author, pivot)

    public Converter()
    {
        Gbx.LZO = new Lzo();
    }

    public bool CheckItems()
    {
        var counter = 0;
        string conversionsFilePath = AppDomain.CurrentDomain.BaseDirectory + @"conversions.json";
        string baseItemsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Trackmania", "Items", "0-B-NoUpload", "MacroblockConverter"
            );
        conversions = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(conversionsFilePath));
        foreach (var conversion in conversions.Values)
        {
            foreach (KeyValuePair<string, string> entry in conversion)
            {
                counter++;
                var itemPath = Path.Combine(baseItemsPath, entry.Value);
                if (File.Exists(itemPath))
                {
                    var item = Gbx.ParseNode<CGameItemModel>(itemPath);
                    Vec3 pivot = new Vec3();
                    if (item.DefaultPlacement.PivotPositions.Length > 0)
                    {
                        pivot = item.DefaultPlacement.PivotPositions[0] * item.DefaultPlacement.PivotSnapDistance;
                    }
                    itemInfo.Add(entry.Key, (item.Ident.Author, pivot));
                }
            }
        }
        return counter == itemInfo.Count;
    } 

    public void Convert(List<string> sourceFiles, bool preserveTrimmed, bool nullifyVariants, bool createConvertedFolder, bool convertBlocksToItems, List<string> convertOptions, Action<string> log)
    {
        int totalSkipped = 0;

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
                log("Didn't find 'Blocks' directory in block path, please work within the standard game filesystem.");
                return;
            }
            try
            {
                var macroBlock = Gbx.ParseNode<CGameCtnMacroBlockInfo>(sourceFile);
                var sourceEnv = macroBlock.Ident.Collection.Number;
                macroBlock.AutoTerrains = [];
                var validBlocks = CollectValidBlocks(macroBlock, nullifyVariants);
                if (convertBlocksToItems) { 
                    macroBlock.ObjectSpawns.AddRange(CollectConvertibleBlocks(macroBlock.BlockSpawns, convertOptions));
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
                    if (sourceEnv == collectionId) { continue; }  // don't convert to the same environment
                    string destFolder = Path.Combine(baseBlocksPath, envName, createConvertedFolder ? "Converted" : "");
                    string destPath = Path.Combine(destFolder, relativePath);
                    string destDirectory = Path.GetDirectoryName(destPath);

                    if (!Directory.Exists(destDirectory))
                    {
                        Directory.CreateDirectory(destDirectory);
                    }

                    foreach (var block in validBlocks)
                    {
                        var blockName = block.BlockModel.Id;

                        if (blockName.Equals("DecoWallBase"))
                        {
                            blockName = "DecoWallBasePillar";
                        } else if (blockName.Equals("DecoWallBasePillar") && collectionId == 26)  // we are converting from a vista back to stadium
                        {
                            blockName = "DecoWallBase";
                        }
                        block.BlockModel = new Ident(
                            blockName,
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

    private List<CGameCtnMacroBlockInfo.ObjectSpawn> CollectConvertibleBlocks(List<CGameCtnMacroBlockInfo.BlockSpawn> blockSpawns, List<string> convertOptions)
    {
        var objectSpawns = new List<CGameCtnMacroBlockInfo.ObjectSpawn>();
        Dictionary<string, string> blockToItem = conversions
            .Where(entry => convertOptions.Contains(entry.Key))
            .SelectMany(entry => entry.Value)
            .ToDictionary();
        foreach (var block in blockSpawns)
        {
            // check if there is a corresponding item
            if (blockToItem.ContainsKey(block.BlockModel.Id))
            {
                var objectSpawn = new CGameCtnMacroBlockInfo.ObjectSpawn();
                var itemPath = "0-B-NoUpload/MacroblockConverter/" + blockToItem[block.BlockModel.Id];
                (var author, var pivot) = itemInfo[block.BlockModel.Id];
                objectSpawn.ItemModel = new Ident(itemPath.Replace('/', '\\'), 26, author);
                objectSpawn.PivotPosition = pivot;
                var placementMode = block.Flags >> 25;
                if (placementMode <= 1)  // normal / ghost
                {
                    var pitch = block.Direction switch
                    {
                        Direction.North => 0,
                        Direction.East => -Math.PI / 2,
                        Direction.South => -Math.PI,
                        Direction.West => Math.PI / 2,
                    };
                    objectSpawn.PitchYawRoll = new Vec3((float)pitch, 0, 0);
                    objectSpawn.BlockCoord = block.Coord;
                    objectSpawn.AbsolutePositionInMap = block.Coord * (32, 8, 32) - objectSpawn.PivotPosition;
                } else  // freeblock
                {
                    objectSpawn.AbsolutePositionInMap = block.AbsolutePositionInMap - objectSpawn.PivotPosition;
                    objectSpawn.BlockCoord = new Int3(
                        (int)Math.Floor((double)objectSpawn.AbsolutePositionInMap.X / 32),
                        (int)Math.Floor((double)objectSpawn.AbsolutePositionInMap.Y / 8),
                        (int)Math.Floor((double)objectSpawn.AbsolutePositionInMap.Z / 32)
                        );
                    objectSpawn.PitchYawRoll = block.PitchYawRoll;
                }
                objectSpawn.Scale = 1;
                objectSpawn.Version = 14;
                objectSpawn.U03 = 1;
                objectSpawn.U04 = new Int3(-1, -1, -1);
                objectSpawn.U05 = (byte)block.U02;  // assign color
                objectSpawn.U10 = -1;
                objectSpawns.Add(objectSpawn);
            }
        }
        return objectSpawns;
    }

    private List<CGameCtnMacroBlockInfo.BlockSpawn> CollectValidBlocks(CGameCtnMacroBlockInfo macroBlock, bool nullifyVariants)
    {
        var blockSpawns = macroBlock.BlockSpawns;
        var validBlocks = new List<CGameCtnMacroBlockInfo.BlockSpawn>();

        foreach (var block in blockSpawns)
        {
            if (block.BlockModel == null)
                continue;

            if (block.BlockModel.Author != "Nadeo")
                continue;

            string blockName = block.BlockModel.Id;
            bool isValid = true;

            foreach (var token in banList)
            {
                if (blockName.Contains(token))
                {
                    isValid = false;
                    break;
                }
            }

            foreach (var token in whiteList)
            {
                if (blockName.Contains(token))
                {
                    isValid = true;
                    break;
                }
            }

            if (isValid)
            {
                // perform name conversions
                // TODO maybe don't convert and change surface?
                if (blockName.StartsWith("DecoPlatformDirt"))
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