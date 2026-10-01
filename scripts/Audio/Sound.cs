using Godot;

namespace AinSoph.Audio;

/// <summary>
/// Music, ambience and sound effects. One instance, added by GameRoot.
/// Sounds are assets/audio/*.ogg, made by tools/make-sounds.py.
///
///   Sound.Play("eat");
/// </summary>
public partial class Sound : Node
{
    public const string MusicBus    = "Music";
    public const string AmbienceBus = "Ambience";
    public const string EffectsBus  = "Effects";

    private static Sound? _instance;

    private AudioStreamPlayer _music    = null!;
    private AudioStreamPlayer _ambience = null!;
    private readonly List<AudioStreamPlayer> _voices = new();
    private readonly Dictionary<string, AudioStream?> _cache = new();
    private readonly Dictionary<string, ulong> _lastPlayedMs = new();

    // Per-effect level (dB) so the set sits together
    private static readonly Dictionary<string, float> Levels = new()
    {
        ["step"] = -16f, ["click"] = -12f, ["speech"] = -14f, ["eat"] = -6f, ["reap"] = -4f,
        ["council"] = -4f, ["rib"] = -4f, ["birth"] = -6f, ["warning"] = -4f, ["death"] = -2f, ["pray"] = -6f,
    };

    public override void _Ready()
    {
        _instance = this;
        EnsureBus(MusicBus);
        EnsureBus(AmbienceBus);
        EnsureBus(EffectsBus);

        _music    = NewPlayer(MusicBus, -10f);
        _ambience = NewPlayer(AmbienceBus, -14f);
        for (int i = 0; i < 8; i++) _voices.Add(NewPlayer(EffectsBus, 0f));

        StartLoop(_music, "music_theme");
        StartLoop(_ambience, "ambience_wind");
    }

    /// <summary>Play a one-shot effect. Repeats of the same sound within 60 ms are dropped.</summary>
    public static void Play(string name, float pitchJitter = 0f)
    {
        var s = _instance;
        if (s == null || !s.IsInsideTree()) return;

        var now = Time.GetTicksMsec();
        if (s._lastPlayedMs.TryGetValue(name, out var last) && now - last < 60) return;
        s._lastPlayedMs[name] = now;

        var stream = s.Load(name);
        if (stream == null) return;
        var voice = s._voices.FirstOrDefault(v => !v.Playing) ?? s._voices[0];
        voice.Stream     = stream;
        voice.VolumeDb   = Levels.TryGetValue(name, out var db) ? db : -8f;
        voice.PitchScale = pitchJitter > 0 ? 1f + (float)GD.RandRange(-pitchJitter, pitchJitter) : 1f;
        voice.Play();
    }

    public static void SetBusVolume(string bus, float linear)
    {
        EnsureBus(bus);
        var idx = AudioServer.GetBusIndex(bus);
        AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(Mathf.Max(linear, 0.0001f)));
        AudioServer.SetBusMute(idx, linear <= 0.001f);
    }

    private static void EnsureBus(string name)
    {
        if (AudioServer.GetBusIndex(name) >= 0) return;
        AudioServer.AddBus();
        var idx = AudioServer.BusCount - 1;
        AudioServer.SetBusName(idx, name);
        AudioServer.SetBusSend(idx, "Master");
    }

    private AudioStreamPlayer NewPlayer(string bus, float volumeDb)
    {
        var p = new AudioStreamPlayer { Bus = bus, VolumeDb = volumeDb };
        AddChild(p);
        return p;
    }

    private void StartLoop(AudioStreamPlayer player, string name)
    {
        if (Load(name) is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = true;
            player.Stream = ogg;
            player.Play();
        }
    }

    private AudioStream? Load(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;
        var path = $"res://assets/audio/{name}.ogg";
        var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        _cache[name] = stream;
        return stream;
    }
}
