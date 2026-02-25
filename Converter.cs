using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.Engines.GameData;
using GBX.NET.Engines.Plug;
using GBX.NET.LZO;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

public class Converter
{
    // blocks that contain these identifiers are filtered out during conversion
    private readonly string[] banList = [
        "Snow", "Rally", "DecoWall", "TrackWall", "Canopy", "Water", "Stage", "Hill", "Cliff", "ToTheme", "Roulette",  // stadium
        "Lake", "River", "Terrain", "Land", "Beach", "Sea", "Shore"  // vistas
    ];

    private readonly string[] whiteList = ["DecoWallBase", "DecoWallSlope2Straight", "DecoWallDiag1", "StageTechnicsLight", "GameplayRally", "GameplaySnow"];

    private readonly Dictionary<string, int> collections = new()
    {
        { "BlueBay", 28 },
        { "GreenCoast", 15 },
        { "RedIsland", 16 },
        { "WhiteShore", 29 },
        { "Stadium", 26 }
    };

    private Dictionary<string, Dictionary<string, string>> conversions = [];
    private readonly Dictionary<string, (string, Vec3, Int3)> itemInfo = [];  // (author, pivot, size)
    private Dictionary<string, string> blockToItem = [];  // mapping between block identifier and item filepath
    private readonly HashSet<string> vegetation = [];  // set of identifiers of vegetation items in vistas
    private readonly Regex terrainRegex = new(@"On(?:Water|Dirt|Lake|Grass|Land|Beach|SeaCliff).*?(?=\d|$)(\d*)", RegexOptions.Compiled);  // regex for replacing road/platform on terrain with regular blocks
    private readonly HashSet<string> safeVariants = ["StructureSupport", "StageTechnicsLight"];  // blocks that are safe to fully convert variants of TODO INCOMPLETE ?
    private readonly List<string> altCars = ["Snow", "Rally", "Desert"];

    public Converter()
    {
        Gbx.LZO = new Lzo();
        vegetation = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + @"vegetation.json"));
        conversions = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + @"conversions.json"));
    }

    public bool CheckItems(Action<string> Log)
    {
        Log("Checking local items...");
        var counter = 0;
        string baseItemsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Trackmania", "Items", "0-B-NoUpload", "MacroblockConverter"
            );
        
        foreach (KeyValuePair<string, Dictionary<string, string>> mapping in conversions)
        {
            foreach (KeyValuePair<string, string> entry in mapping.Value)
            {
                counter++;
                var itemPath = Path.Combine(baseItemsPath, entry.Value);
                if (File.Exists(itemPath))
                {
                    var item = Gbx.ParseNode<CGameItemModel>(itemPath);
                    Vec3 pivot = new Vec3();
                    var size = (0, 0, 0);
                    if (item.DefaultPlacement.PivotPositions.Length > 0)
                    {
                        pivot = item.DefaultPlacement.PivotPositions[0] * item.DefaultPlacement.PivotSnapDistance;
                    } else
                    {
                        // items converted automatically without pivot positions need to be aligned based on their block size
                        CGameCommonItemEntityModelEdition entityModelEdition = (CGameCommonItemEntityModelEdition)item.EntityModelEdition;
                        CPlugCrystal.GeometryLayer geometryLayer = (CPlugCrystal.GeometryLayer)entityModelEdition.MeshCrystal.Layers[0];
                        var meshPositions = geometryLayer.Crystal.Positions;
                        var minx = meshPositions.Select(v => v.X).Min();
                        var maxx = meshPositions.Select(v => v.X).Max();
                        var minz = meshPositions.Select(v => v.Z).Min();
                        var maxz = meshPositions.Select(v => v.Z).Max();
                        size = ((int)Math.Round((maxx - minx) / 32),
                                0,
                                (int)Math.Round((maxz - minz) / 32));
                    }
                    itemInfo[entry.Key] = (item.Ident.Author, pivot, size);
                }
            }
        }
        return counter == itemInfo.Count;
    } 

    public void Convert(List<string> sourceFiles, bool preserveTrimmed, bool nullifyVariants, bool convertGround, bool ignoreVegetation,
        bool convertBlocksToItems, List<string> convertOptions, HashSet<string> targets, Action<string> log)
    {
        int totalSkipped = 0;
        blockToItem = conversions
            .Where(entry => convertOptions.Contains(entry.Key))
            .SelectMany(entry => entry.Value)
            .ToDictionary();

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
                    var relativeParts = parts.Skip(i + 1).ToList();
                    relativeParts[0] += @"Converted";
                    relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), relativeParts);
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
                var sourceCollection = macroBlock.Ident.Collection.Number;
                macroBlock.AutoTerrains = [];
                var validBlocks = CollectValidBlocks(macroBlock, nullifyVariants, convertGround, convertOptions.Contains("Override Vista DecoWall"));
                if (convertBlocksToItems) { 
                    macroBlock.ObjectSpawns.AddRange(ConvertBlocksToItems(macroBlock.BlockSpawns, log));
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
                foreach (var collection in collections)
                {
                    string collectionName = collection.Key;
                    int collectionId = collection.Value;
                    if (!targets.Contains(collectionName)) { continue; }  // skip if user specified to not convert to this collection
                    if (sourceCollection == collectionId) { continue; }  // don't convert to the same collection
                    string destFolder = Path.Combine(baseBlocksPath, collectionName);
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
                    foreach (var objectSpawn in macroBlock.ObjectSpawns)
                    {
                        var ident = objectSpawn.ItemModel;
                        // replace ident of nando items so that they can be selected in the editor, except for vegetation (might not be shared) and alt cars items
                        if (ident.Author == "Nadeo" && !vegetation.Contains(ident.Id) && altCars.All(s => !ident.Id.StartsWith(s)))
                        {
                            objectSpawn.ItemModel = new Ident(ident.Id, collectionId, ident.Author);
                        }
                    }
                    if (ignoreVegetation)
                    {
                        macroBlock.ObjectSpawns.RemoveAll(objectSpawn => {
                            var ident = objectSpawn.ItemModel;
                            return ident.Author == "Nadeo" && vegetation.Contains(ident.Id);
                        });
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

    private List<CGameCtnMacroBlockInfo.ObjectSpawn> ConvertBlocksToItems(List<CGameCtnMacroBlockInfo.BlockSpawn> blockSpawns, Action<string> Log)
    {
        var objectSpawns = new List<CGameCtnMacroBlockInfo.ObjectSpawn>();
        foreach (var block in blockSpawns)
        {
            // check if there is a corresponding item
            if (blockToItem.ContainsKey(block.BlockModel.Id))
            {
                var objectSpawn = new CGameCtnMacroBlockInfo.ObjectSpawn();
                var itemPath = "0-B-NoUpload/MacroblockConverter/" + blockToItem[block.BlockModel.Id];
                (var author, var pivot, var size) = itemInfo[block.BlockModel.Id];
                objectSpawn.ItemModel = new Ident(itemPath.Replace('/', '\\'), 26, author);
                objectSpawn.PivotPosition = pivot;
                var placementMode = block.Flags >> 24;
                if (placementMode < 4)  // 0 = normal / 1 = ground / 2 = air / 3 = ground ghost
                {
                    (double pitch, Int3 offset) = block.Direction switch
                    {
                        Direction.North => (0, (0, 0, 0)),
                        Direction.East => (-Math.PI / 2, new Int3(1, 0, 0) * size.Z),
                        Direction.South => (-Math.PI, new Int3(1, 0, 1) * size.Z),
                        Direction.West => (Math.PI / 2, new Int3(0, 0, 1) * size.Z)
                    };
                    objectSpawn.PitchYawRoll = new Vec3((float)pitch, 0, 0);
                    objectSpawn.BlockCoord = block.Coord + offset;
                    objectSpawn.AbsolutePositionInMap = objectSpawn.BlockCoord * (32, 8, 32) - objectSpawn.PivotPosition;
                } else  // 4 = air ghost (freeblock)
                {
                    (var pitch, var yaw, var roll) = block.PitchYawRoll;

                    Quaternion rotation;
                    Vector3 rotatedPivot;
                    if (MathF.Abs(MathF.Abs(pitch) - MathF.PI / 2) < 0.01)
                    {
                        rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch > 0 ? -(yaw - roll) : (yaw + roll));
                        rotatedPivot = Vector3.Transform(pivot, rotation);
                        objectSpawn.AbsolutePositionInMap = block.AbsolutePositionInMap + 
                            (pitch > 0 ? (rotatedPivot.X, -rotatedPivot.Y, -rotatedPivot.Z) : (-rotatedPivot.X, -rotatedPivot.Y, rotatedPivot.Z));
                    }
                    else
                    {
                        rotation = Quaternion.CreateFromYawPitchRoll(pitch, yaw, roll);
                        rotatedPivot = Vector3.Transform(pivot * -1, rotation);
                        objectSpawn.AbsolutePositionInMap = block.AbsolutePositionInMap + (rotatedPivot.X, rotatedPivot.Y, rotatedPivot.Z);
                    }
                    objectSpawn.BlockCoord = new Int3(
                        (int)Math.Floor((double)objectSpawn.AbsolutePositionInMap.X / 32),
                        (int)Math.Floor((double)objectSpawn.AbsolutePositionInMap.Y / 8),
                        (int)Math.Floor((double)objectSpawn.AbsolutePositionInMap.Z / 32)
                        );
                    objectSpawn.PitchYawRoll = block.PitchYawRoll;
                    // Log($"{block.BlockModel.Id} ({block.PitchYawRoll}): {block.AbsolutePositionInMap} + {rotatedPivot} = {objectSpawn.AbsolutePositionInMap}");
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

    private List<CGameCtnMacroBlockInfo.BlockSpawn> CollectValidBlocks(CGameCtnMacroBlockInfo macroBlock, bool nullifyVariants, bool convertGround, bool skipVistaDecoWall)
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
            if (skipVistaDecoWall && conversions["Override Vista DecoWall"].ContainsKey(blockName))
                continue;
            var placementMode = block.Flags >> 24;
            // replace blocks on terrain with normal blocks
            var match = terrainRegex.Match(blockName);
            if (match.Success)
            {
                blockName = blockName.Replace(match.ToString(), string.Empty);
                if (blockName.Contains("Slope")) // slopes don't need y fixing
                {
                    if (blockName.Contains("SlopeBase"))  // these are mirrored
                    {
                        if (placementMode < 4)
                        {
                            block.Direction = block.Direction switch
                            {
                                Direction.North => Direction.South,
                                Direction.East => Direction.West,
                                Direction.South => Direction.North,
                                Direction.West => Direction.East,
                            };
                        }
                        else 
                        {
                            (var pitch, var yaw, var roll) = block.PitchYawRoll;
                            block.PitchYawRoll = new Vec3(-pitch, yaw >= 0 ? yaw - (float)Math.PI : yaw + (float)Math.PI, -roll);
                        }
                    }
                    if (blockName.Contains("Platform")) // only platform has different slope2 names
                    {
                        blockName = blockName.Replace("SlopeBase2", "Slope2Base");
                    }
                } else
                {
                    if (placementMode == 4) continue;  // TODO implement correct transformation for freeblocked "OnTerrain" blocks
                    if (blockName.Contains("Diag"))
                    {
                        blockName += "X2";
                    }
                    // add terrain height to the block y coordinate
                    int blockHeight = !string.IsNullOrEmpty(match.Groups[1].Value) ? int.Parse(match.Groups[1].Value) : 1;  // if there's no number in string, offset is 1
                    block.Coord = new Int3(block.Coord.X, block.Coord.Y + blockHeight, block.Coord.Z);
                }
            }

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
                if (nullifyVariants && safeVariants.All(s => !blockName.Contains(s)))
                {
                    block.Flags = block.Flags & 0xF000000;  // preserve only placement mode
                }
                if (convertGround && ( placementMode == 1 || placementMode == 3))
                {
                    block.Flags = (2 << 24) + block.Flags & 0x0FFFFFF;  // convert ground / ground ghost mode to air mode
                }
                validBlocks.Add(block);
            }
        }

        return validBlocks;
    }
}