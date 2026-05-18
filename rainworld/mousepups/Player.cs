using UnityEngine;
using BepInEx.Logging;
using System.IO;
using RWCustom;
using MoreSlugcats;

namespace MicePupsMod;

public partial class MicePupsMod 
{
    private void PlayerOnDie(On.Player.orig_Die orig, Player self)
    {
        bool wasAlive = !self.dead;
    
        orig(self);

        if (!wasAlive || self.isNPC || self.room == null) return;

        SpawnSingularityBomb(self.room, self.mainBodyChunk.pos);

        Logger.LogInfo("Spawned a SingularityBomb on death!");
    }

    private void SpawnSingularityBomb(Room room, Vector2 pixelPos)
    {
        // Convert to precise tile coordinates
        IntVector2 tilePos = room.GetTilePosition(pixelPos);
        
        // Find nearest valid air tile (with 5-tile radius search)
        if (room.GetTile(tilePos).Solid)
        {
            IntVector2? foundTile = FindNearestAirTile(tilePos, 5);
            if (foundTile.HasValue) tilePos = foundTile.Value;
            else tilePos = new IntVector2(room.TileWidth/2, room.TileHeight/2);
        }

        IntVector2 FindNearestAirTile(IntVector2 start, int maxRadius)
        {
            for (int r = 0; r < maxRadius; r++)
            {
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        IntVector2 checkPos = new IntVector2(start.x + x, start.y + y);
                        if (room.GetTile(checkPos).Terrain == Room.Tile.TerrainType.Air)
                            return checkPos;
                    }
                }
            }
            return new IntVector2(room.TileWidth/2, room.TileHeight/2); // Fallback to the center of the room
        }

        // Create with precise world coordinate
        WorldCoordinate spawnCoord = new WorldCoordinate(room.abstractRoom.index, tilePos.x, tilePos.y, -1);
        
        var abstractBomb = new AbstractPhysicalObject(
            room.world,
            DLCSharedEnums.AbstractObjectType.SingularityBomb,
            null,
            spawnCoord,
            room.game.GetNewID()
        );

        // Add to room
        room.abstractRoom.AddEntity(abstractBomb);
        
        // Realize and force position
        abstractBomb.RealizeInRoom();
        
        if (abstractBomb.realizedObject is SingularityBomb bomb)
        {
            // Convert back to precise pixel position
            Vector2 spawnPos = room.MiddleOfTile(tilePos) + new Vector2(0, 20f);
            bomb.firstChunk.HardSetPosition(spawnPos);
            bomb.firstChunk.lastPos = spawnPos; // Prevent physics correction
            
            Debug.Log($"Bomb spawned at {spawnPos} (world coord: {spawnCoord})");

            // owo
            bomb.Explode();
        }

    }

    private void PlayerOnUpdate(On.Player.orig_Update orig, Player self, bool eu)
    {
        orig(self, eu); //Always call original code, either before or after your code, depending on what you need to achieve

        //self.slugcatStats.runspeedFac += 0.01f;

        //Logger.LogInfo("The player is running!");

        if (self.canJump == 0) {
            if (self.graphicsModule is PlayerGraphics graphics) {
                graphics.markAlpha = 1f;
            }
        }
    }

    private void PlayerActivateMark(On.Player.orig_Jump orig, Player self) {
        orig(self);

        if (self.graphicsModule is PlayerGraphics graphics) {
            (self.abstractCreature.world.game.session as StoryGameSession).saveState.deathPersistentSaveData.theMark = true;
            graphics.lastMarkAlpha = 1f;
            graphics.markAlpha = 1f;
        }
    }
}