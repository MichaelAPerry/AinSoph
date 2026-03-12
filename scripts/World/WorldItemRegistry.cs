using System;
using System.Collections.Generic;
using System.Linq;
using AinSoph.Data;
using AinSoph.Items;
using Godot;

namespace AinSoph.World
{
    /// <summary>
    /// Live list of all items currently in the world.
    /// Handles spawning, decay, and spatial queries.
    ///
    /// Items are persisted via SaveManager. This class is the in-memory view.
    /// </summary>
    public class WorldItemRegistry
    {
        private readonly List<ItemSaveData> _items = new();
        private readonly SaveManager        _save;

        public IReadOnlyList<ItemSaveData> All => _items;

        public WorldItemRegistry(SaveManager save)
        {
            _save = save;
        }

        // ── Loading ───────────────────────────────────────────────────────

        public void LoadAll()
        {
            _items.Clear();
            foreach (var item in _save.LoadAllItems())
                _items.Add(item);
            GD.Print($"WorldItemRegistry: loaded {_items.Count} items");
        }

        // ── Spawn ─────────────────────────────────────────────────────────

        public ItemSaveData Spawn(string name, string type, int tileX, int tileY,
                                  bool edible = false, float? lifespanHours = null,
                                  string description = "")
        {
            var item = new ItemSaveData
            {
                Id           = $"{type}.{Guid.NewGuid():N}",
                Name         = name,
                Type         = type,
                Edible       = edible,
                LifespanHours = lifespanHours,
                AgeHours     = 0f,
                TileX        = tileX,
                TileY        = tileY,
                Description  = description,
            };
            _items.Add(item);
            _save.SaveItem(item);
            return item;
        }

        /// <summary>Spawn a body item at a tile — used on NPC/animal/player death.</summary>
        public ItemSaveData SpawnBody(string entityName, int tileX, int tileY)
        {
            return Spawn(
                name:         $"Body of {entityName}",
                type:         "body",
                tileX:        tileX,
                tileY:        tileY,
                edible:       false,
                lifespanHours: null,   // bodies persist until acted on
                description:  $"The body of {entityName} lies here."
            );
        }

        /// <summary>Spawn a manna item. Manna is edible, lasts 24 real hours.</summary>
        public ItemSaveData SpawnManna(int tileX, int tileY)
        {
            return Spawn(
                name:         "Manna",
                type:         "manna",
                tileX:        tileX,
                tileY:        tileY,
                edible:       true,
                lifespanHours: 24f,
                description:  "A portion of manna. It will not last."
            );
        }

        /// <summary>
        /// Consume an item — removes it from the registry and returns true.
        /// Returns false if the item doesn't exist or isn't edible.
        /// </summary>
        public bool Consume(string itemId)
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return false;
            Remove(itemId);
            return true;
        }

        // ── Removal ───────────────────────────────────────────────────────

        public void Remove(string itemId)
        {
            _items.RemoveAll(i => i.Id == itemId);
            _save.DeleteItem(itemId);
        }

        // ── Decay tick — call hourly ───────────────────────────────────────

        /// <summary>
        /// Ages all living items by deltaHours.
        /// Removes and deletes any that have expired.
        /// Returns the list of expired item ids.
        /// </summary>
        public List<string> TickDecay(float deltaHours)
        {
            var expired = new List<string>();

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                if (item.LifespanHours == null) continue;

                item.AgeHours = (item.AgeHours ?? 0f) + deltaHours;
                if (item.AgeHours >= item.LifespanHours)
                {
                    expired.Add(item.Id);
                    _items.RemoveAt(i);
                    _save.DeleteItem(item.Id);
                }
                else
                {
                    _save.SaveItem(item); // persist updated age
                }
            }

            return expired;
        }

        // ── Spatial queries ───────────────────────────────────────────────

        /// <summary>All items at or adjacent to a tile (within 1 tile).</summary>
        public List<ItemSaveData> NearTile(int tileX, int tileY, int radius = 1)
        {
            return _items.Where(i =>
                Math.Abs(i.TileX - tileX) <= radius &&
                Math.Abs(i.TileY - tileY) <= radius
            ).ToList();
        }

        /// <summary>First edible item within reach of a tile. Null if none.</summary>
        public ItemSaveData? NearestEdible(int tileX, int tileY, int radius = 1)
        {
            return _items
                .Where(i => i.Edible &&
                            Math.Abs(i.TileX - tileX) <= radius &&
                            Math.Abs(i.TileY - tileY) <= radius)
                .OrderBy(i => Math.Abs(i.TileX - tileX) + Math.Abs(i.TileY - tileY))
                .FirstOrDefault();
        }

        /// <summary>All items in a given cell (8×8 tile block).</summary>
        public List<ItemSaveData> InCell(int cellX, int cellY)
        {
            int minTile = cellX * 8;
            int maxTile = minTile + 7;
            int minTileY = cellY * 8;
            int maxTileY = minTileY + 7;
            return _items.Where(i =>
                i.TileX >= minTile && i.TileX <= maxTile &&
                i.TileY >= minTileY && i.TileY <= maxTileY
            ).ToList();
        }
    }
}
