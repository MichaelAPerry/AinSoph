using AinSoph.NPC;
using AinSoph.Player;
using AinSoph.Skills;
using Godot;

namespace AinSoph;

/// <summary>
/// What the player carries. Move on an item beside you picks it up; the pack
/// (the PACK button, or I) lets you eat, give or drop what you hold. A carried
/// thing the engine can read as a gift (a lantern, a spear — see Gift.cs) works
/// while you hold it, and goes with it when you give it away or drop it. Food
/// still spoils in the pack. When you die, what you carried falls where you fell.
/// </summary>
public partial class GameRoot
{
    private AinSoph.UI.PackScreen? _pack;

    /// <summary>Pick up an item from the ground. False if it cannot be carried.</summary>
    public bool PickUp(Data.ItemSaveData item)
    {
        if (Player == null || Items == null || _worldScene == null) return false;
        if (item.Type == "body" && !item.Edible)
        {
            _worldScene.ShowWorldText("It is too heavy to carry, and not yours to take.");
            return false;
        }
        if (Player.Carried.Count >= PlayerCharacter.MaxCarried)
        {
            _worldScene.ShowWorldText($"You can carry no more than {PlayerCharacter.MaxCarried} things.");
            return false;
        }

        Items.Remove(item.Id);
        Player.Carried.Add(new Data.CarriedItem { Item = item, PickedUpUtc = DateTime.UtcNow });

        var gift = GiftFromItem(item);
        if (gift != null)
        {
            var already = Player.Gifts.Has(gift.Effect);
            Player.Gifts.Add(gift);
            _worldScene.SetGifts(Player.Gifts.All);
            _worldScene.ShowWorldText($"You take the {item.Name} — {Gifts.Describe(gift.Effect)}" +
                                      (already ? " (another gift already does this)." : ", while you carry it."));
            _worldScene.RefreshMap(); // Sight changes what you see
        }
        else
            _worldScene.ShowWorldText($"You take the {item.Name}.");

        AinSoph.Audio.Sound.Play("click");
        _worldScene.RefreshMap();
        _pack?.Refresh();
        SaveAll();
        return true;
    }

    /// <summary>A made thing the engine can read as a gift works while carried; manna and meat are only food.</summary>
    private static Gift? GiftFromItem(Data.ItemSaveData item)
    {
        if (item.Type is "manna" or "body") return null;
        var effect = Gifts.Classify($"{item.Name} {item.Description}");
        return effect == GiftEffect.Lore ? null : new Gift
        {
            Name = item.Name, Description = item.Description, Kind = "item",
            Effect = effect, GrantedUtc = DateTime.UtcNow, ItemId = item.Id,
        };
    }

    private void LoseCarriedGift(string itemId)
    {
        if (Player == null) return;
        Player.Gifts.All.RemoveAll(g => g.ItemId == itemId);
        _worldScene?.SetGifts(Player.Gifts.All);
    }

    public void PackEat(Data.CarriedItem carried)
    {
        if (Player == null || _worldScene == null || !Player.Carried.Contains(carried) || !carried.Item.Edible) return;
        Player.Carried.Remove(carried);
        LoseCarriedGift(carried.Item.Id);
        Player.Survival.RecordEat(DateTime.UtcNow);
        _worldScene.ShowWorldText($"You eat the {carried.Item.Name}. The hunger recedes.");
        AinSoph.Audio.Sound.Play("eat");
        JudgeDeed($"ate {carried.Item.Name}.");
        _pack?.Refresh();
        SaveAll();
    }

    public void PackDrop(Data.CarriedItem carried)
    {
        if (Player == null || Items == null || _worldScene == null || !Player.Carried.Remove(carried)) return;
        LoseCarriedGift(carried.Item.Id);
        var (x, y) = FreeItemTileNear(Player.TileX, Player.TileY);
        Respawn(carried, x, y);
        _worldScene.ShowWorldText($"You set down the {carried.Item.Name}.");
        _worldScene.RefreshMap();
        _pack?.Refresh();
        SaveAll();
    }

    /// <summary>The NPC beside you, if any — who a gift would go to.</summary>
    public NpcBrain? NpcBeside() =>
        Player == null ? null : LiveNpcs.Where(n => Math.Max(Math.Abs(n.TileX - Player.TileX), Math.Abs(n.TileY - Player.TileY)) <= 1)
                                        .OrderBy(n => Math.Abs(n.TileX - Player.TileX) + Math.Abs(n.TileY - Player.TileY))
                                        .FirstOrDefault();

    public void PackGive(Data.CarriedItem carried)
    {
        if (Player == null || _worldScene == null) return;
        var npc = NpcBeside();
        if (npc == null) { _worldScene.ShowWorldText("No one is beside you to take it."); return; }
        if (!Player.Carried.Remove(carried)) return;
        LoseCarriedGift(carried.Item.Id);

        var item = carried.Item;
        if (item.Edible)
        {
            npc.Survival.RecordEat(DateTime.UtcNow);
            npc.Memory.Write(MemorySlot.Feeling, $"{Player.Name} gave me {item.Name} when I needed food.");
            _worldScene.ShowWorldText($"{npc.Name} takes the {item.Name} and eats.");
        }
        else
        {
            if (GiftFromItem(item) is { } gift) npc.Gifts.Add(gift);
            npc.Memory.Write(MemorySlot.Feeling, $"{Player.Name} gave me {item.Name}.");
            _worldScene.ShowWorldText($"You give the {item.Name} to {npc.Name}.");
        }
        _worldScene.ShowNpcSpeech(npc.NpcId, "…");
        AinSoph.Audio.Sound.Play("click");
        _pack?.Refresh();
        SaveAll();
    }

    /// <summary>Food carried too long spoils, as it would on the ground.</summary>
    private void SpoilPack()
    {
        if (Player == null) return;
        var now = DateTime.UtcNow;
        var spoiled = Player.Carried.Where(c => c.Spoiled(now)).ToList();
        foreach (var c in spoiled)
        {
            Player.Carried.Remove(c);
            LoseCarriedGift(c.Item.Id);
            _worldScene?.ShowWorldText($"The {c.Item.Name} you carried has spoiled.");
        }
        if (spoiled.Count > 0) _pack?.Refresh();
    }

    /// <summary>On death: what you carried falls where you fell.</summary>
    private void SpillPack(int x, int y)
    {
        if (Player == null) return;
        foreach (var c in Player.Carried.ToList())
        {
            var (tx, ty) = FreeItemTileNear(x, y);
            Respawn(c, tx, ty);
        }
        Player.Carried.Clear();
    }

    private void Respawn(Data.CarriedItem c, int x, int y)
    {
        var age = (float)((c.Item.AgeHours ?? 0) + (DateTime.UtcNow - c.PickedUpUtc).TotalHours);
        var item = Items!.Spawn(c.Item.Name, c.Item.Type, x, y, c.Item.Edible, c.Item.LifespanHours, c.Item.Description);
        item.AgeHours = age;
    }

    private (int X, int Y) FreeItemTileNear(int x, int y)
    {
        for (int r = 0; r <= 3; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            int tx = x + dx, ty = y + dy;
            if (IsPassableTile(tx, ty) && !Items!.All.Any(i => i.TileX == tx && i.TileY == ty)) return (tx, ty);
        }
        return (x, y);
    }

    // ── The pack screen ──────────────────────────────────────────────────

    private void OpenPack()
    {
        if (Player == null || _worldScene == null || string.IsNullOrEmpty(Player.Name)) return;
        if (_pack != null) { ClosePack(); return; }
        SpoilPack();
        _pack = new AinSoph.UI.PackScreen(() => Player?.Carried ?? new(), () => NpcBeside()?.Name,
                                          PackEat, PackGive, PackDrop, ClosePack);
        AddChild(_pack);
        _worldScene.InputLocked = true;
    }

    private void ClosePack()
    {
        _pack?.QueueFree();
        _pack = null;
        if (_worldScene != null) _worldScene.InputLocked = false;
    }
}
