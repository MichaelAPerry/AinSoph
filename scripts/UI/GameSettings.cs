using Godot;

namespace AinSoph.UI;

/// <summary>
/// Player settings, saved to user://settings.cfg.
/// Volumes are linear 0–1 and applied to the Music, Ambience and Effects buses.
/// </summary>
public static class GameSettings
{
    private const string Path = "user://settings.cfg";
    private static readonly ConfigFile Cfg = new();
    private static bool _loaded;

    public static bool  Fullscreen     { get; set; }
    public static float MusicVolume    { get; set; } = 0.6f;
    public static float AmbienceVolume { get; set; } = 0.7f;
    public static float EffectsVolume  { get; set; } = 0.8f;
    public static bool  ShowHints      { get; set; } = true;
    public static int   HintsSeen      { get; set; }

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        if (Cfg.Load(Path) != Error.Ok) return;
        Fullscreen     = (bool) Cfg.GetValue("display", "fullscreen", false);
        MusicVolume    = (float)Cfg.GetValue("audio", "music", 0.6f);
        AmbienceVolume = (float)Cfg.GetValue("audio", "ambience", 0.7f);
        EffectsVolume  = (float)Cfg.GetValue("audio", "effects", 0.8f);
        ShowHints      = (bool) Cfg.GetValue("guide", "show_hints", true);
        HintsSeen      = (int)  Cfg.GetValue("guide", "hints_seen", 0);
    }

    public static void Save()
    {
        Cfg.SetValue("display", "fullscreen", Fullscreen);
        Cfg.SetValue("audio", "music", MusicVolume);
        Cfg.SetValue("audio", "ambience", AmbienceVolume);
        Cfg.SetValue("audio", "effects", EffectsVolume);
        Cfg.SetValue("guide", "show_hints", ShowHints);
        Cfg.SetValue("guide", "hints_seen", HintsSeen);
        Cfg.Save(Path);
    }

    /// <summary>Apply display and audio settings to the running game.</summary>
    public static void Apply()
    {
        if (!OS.HasFeature("headless") && DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        Audio.Sound.SetBusVolume(Audio.Sound.MusicBus,    MusicVolume);
        Audio.Sound.SetBusVolume(Audio.Sound.AmbienceBus, AmbienceVolume);
        Audio.Sound.SetBusVolume(Audio.Sound.EffectsBus,  EffectsVolume);
    }

    public static string Version =>
        (string)ProjectSettings.GetSetting("application/config/version", "dev");
}
