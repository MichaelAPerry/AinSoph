using AinSoph.NPC;
using AinSoph.World;
using Godot;

namespace AinSoph.Data;

/// <summary>
/// Background LLM queue for NPC think ticks.
/// All NPCs tick fully — the world does not pause for unloaded cells.
///
/// Priority: NPCs near the player go to the front.
/// Cap: if the full population cannot cycle within 45 minutes,
///      NPCs are dropped from the bottom until the load fits the hardware.
///      The world scales to the machine.
///
/// One NPC is processed at a time. The LLM is not parallelized.
/// </summary>
public class NpcTickQueue
{
    private readonly LinkedList<NpcBrain> _queue  = new();
    private readonly HashSet<string>      _queued = new();
    private readonly object               _lock   = new();

    private static readonly TimeSpan QueueCap = TimeSpan.FromMinutes(45);

    // Approximate time per NPC inference. Updated from real measurements.
    // Initial estimate: 45 seconds on a potato CPU.
    private TimeSpan _estimatedTimePerNpc = TimeSpan.FromSeconds(45);

    private bool      _running;
    private NpcBrain? _currentNpc;

    public int QueueDepth   => _queue.Count;
    public int MaxNpcsInCap => (int)(QueueCap / _estimatedTimePerNpc);

    // -------------------------------------------------------------------------
    // Registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Register an NPC with the queue. Priority = true puts them at the front.
    /// Called when NPCs are loaded or when a player moves near them.
    /// </summary>
    public void Enqueue(NpcBrain npc, bool priority = false)
    {
        lock (_lock)
        {
            if (_queued.Contains(npc.NpcId)) return;

            if (priority)
                _queue.AddFirst(npc);
            else
                _queue.AddLast(npc);

            _queued.Add(npc.NpcId);
            EnforceCap();
        }
    }

    public void Remove(string npcId)
    {
        lock (_lock)
        {
            var node = _queue.First;
            while (node is not null)
            {
                if (node.Value.NpcId == npcId)
                {
                    _queue.Remove(node);
                    _queued.Remove(npcId);
                    return;
                }
                node = node.Next;
            }
        }
    }

    /// <summary>
    /// Promote all NPCs in a given cell to the front of the queue.
    /// Called when a player moves near a cell.
    /// </summary>
    public void Prioritize(IEnumerable<string> npcIds)
    {
        lock (_lock)
        {
            var ids = new HashSet<string>(npcIds);
            var toPromote = new List<NpcBrain>();
            var node = _queue.Last;

            while (node is not null)
            {
                var prev = node.Previous;
                if (ids.Contains(node.Value.NpcId))
                {
                    toPromote.Add(node.Value);
                    _queue.Remove(node);
                }
                node = prev;
            }

            foreach (var npc in toPromote)
                _queue.AddFirst(npc);
        }
    }

    // -------------------------------------------------------------------------
    // Run — called from the hourly world tick
    // -------------------------------------------------------------------------

    /// <summary>
    /// Process the next NPC in the queue. Returns immediately if already running.
    /// Should be called from a background thread or async context.
    /// </summary>
    public async Task ProcessNextAsync(
        Func<NpcBrain, SituationContext> buildSituation,
        CancellationToken ct = default)
    {
        NpcBrain? npc;
        lock (_lock)
        {
            if (_queue.Count == 0) return;
            npc = _queue.First!.Value;
            _queue.RemoveFirst();
            _queued.Remove(npc.NpcId);
        }

        _currentNpc = npc;
        var started = DateTime.UtcNow;

        try
        {
            var situation = buildSituation(npc);
            await npc.TickAsync(situation, ct);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"NpcTickQueue: error ticking {npc.NpcId}: {ex.Message}");
        }
        finally
        {
            // Update timing estimate (rolling average)
            var elapsed = DateTime.UtcNow - started;
            _estimatedTimePerNpc = TimeSpan.FromSeconds(
                (_estimatedTimePerNpc.TotalSeconds * 0.8) + (elapsed.TotalSeconds * 0.2));

            _currentNpc = null;

            // Re-enqueue at the back for the next cycle
            lock (_lock)
            {
                if (!ct.IsCancellationRequested)
                    Enqueue(npc, priority: false);

                EnforceCap();
            }
        }
    }

    // -------------------------------------------------------------------------
    // Cap enforcement
    // -------------------------------------------------------------------------

    private void EnforceCap()
    {
        var max = MaxNpcsInCap;
        while (_queue.Count > max && _queue.Last is not null)
        {
            var dropped = _queue.Last.Value;
            _queue.RemoveLast();
            _queued.Remove(dropped.NpcId);
            GD.Print($"NpcTickQueue: queue cap reached — dropped {dropped.NpcId} " +
                     $"(max {max} NPCs at ~{_estimatedTimePerNpc.TotalSeconds:F0}s each)");
        }
    }

    public NpcBrain? CurrentlyProcessing => _currentNpc;
}
