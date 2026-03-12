using AinSoph.LLM;
using AinSoph.NPC;
using Godot;

namespace AinSoph;

/// <summary>
/// Autoloaded singleton. Initializes all engine-level systems at startup.
/// Add this node to Godot's AutoLoad list as "AinSoph".
/// </summary>
public partial class GameRoot : Node
{
    public static LlmRunner Llm { get; private set; } = new();

    // Model path — expected in the user data folder at runtime.
    // Players download the model separately and place it here.
    private const string ModelSubPath = "user://models/qwen2.5-3b.gguf";

    public override void _Ready()
    {
        GD.Print("Ain Soph — initializing");

        DecanRegistry.Load("res://data/ain_soph_72.json");

        var modelPath = ProjectSettings.GlobalizePath(ModelSubPath);
        if (System.IO.File.Exists(modelPath))
        {
            Llm.Initialize(modelPath);
        }
        else
        {
            GD.PrintErr($"GameRoot: LLM model not found at {ModelSubPath}. " +
                        $"NPC dialogue and Council evaluation will be unavailable until the model is placed there.");
        }
    }

    public override void _ExitTree()
    {
        Llm.Dispose();
    }
}
