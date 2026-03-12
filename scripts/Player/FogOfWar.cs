using AinSoph.World;

namespace AinSoph.Player;

/// <summary>
/// Calculates fog of war from the player's current position.
/// Vision range: 3 cells in daylight, 1 at night.
/// Fog is always active beyond vision range — no persistent reveal.
/// At vision edge: biome and landmark shapes visible, no detail.
/// </summary>
public static class FogOfWar
{
    public enum TileVisibility { Clear, Edge, Fog }

    public record CellVisibility(
        string         CellId,
        int            GridX,
        int            GridY,
        TileVisibility Visibility,
        BiomeType?     Biome       // visible even at edge
    );

    /// <summary>
    /// Returns visibility state for all cells in a bounding box around the player.
    /// </summary>
    public static List<CellVisibility> Calculate(
        int playerGridX, int playerGridY, bool isNight, WorldGrid grid)
    {
        var range   = WorldClock.VisionRange(); // 3 day, 1 night
        var results = new List<CellVisibility>();

        // Scan a box large enough to include edge cells
        for (var dx = -(range + 1); dx <= (range + 1); dx++)
        for (var dy = -(range + 1); dy <= (range + 1); dy++)
        {
            var gx   = playerGridX + dx;
            var gy   = playerGridY + dy;
            var dist = Math.Max(Math.Abs(dx), Math.Abs(dy)); // Chebyshev distance

            TileVisibility vis;
            BiomeType?     biome = null;

            if (dist <= range)
            {
                vis   = TileVisibility.Clear;
                biome = grid.GetIfLoaded(gx, gy)?.Biome;
            }
            else if (dist == range + 1)
            {
                vis   = TileVisibility.Edge;
                biome = grid.GetIfLoaded(gx, gy)?.Biome; // biome visible at edge
            }
            else
            {
                vis = TileVisibility.Fog;
            }

            results.Add(new CellVisibility($"{gx},{gy}", gx, gy, vis, biome));
        }

        return results;
    }

    /// <summary>
    /// Determine whether a specific tile within a visible cell is a landmark.
    /// Landmarks (trees, caves, altar) are visible at the edge of vision.
    /// </summary>
    public static bool IsLandmark(Tile tile) =>
        tile.HasCave ||
        tile.Surface == TileSurface.TreeCedar ||
        tile.Surface == TileSurface.TreeOlive ||
        tile.Surface == TileSurface.TreeFig   ||
        tile.Surface == TileSurface.TreePalm;
}
