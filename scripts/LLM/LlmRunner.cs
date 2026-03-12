using LLama;
using LLama.Common;
using Godot;

namespace AinSoph.LLM;

/// <summary>
/// Manages a single loaded Qwen 2.5 3B model instance.
/// Used by NPCs and the Triune Council.
/// CPU-only. Low-spec baseline: 8GB RAM.
///
/// Call Initialize() once at startup with the path to the .gguf model file.
/// Then call InferAsync() to get a completion for a given system prompt + user prompt.
/// </summary>
public class LlmRunner : IDisposable
{
    private LLamaWeights? _weights;
    private ModelParams? _params;
    private bool _ready;

    public bool IsReady => _ready;

    /// <summary>
    /// Load the model from a .gguf file.
    /// modelPath: path to the Qwen 2.5 3B .gguf file.
    /// contextSize: token context window. 2048 is sufficient for NPC and Council use.
    /// </summary>
    public void Initialize(string modelPath, uint contextSize = 2048)
    {
        if (!FileAccess.FileExists(modelPath))
        {
            GD.PrintErr($"LlmRunner: model file not found at {modelPath}");
            return;
        }

        _params = new ModelParams(modelPath)
        {
            ContextSize = contextSize,
            GpuLayerCount = 0,   // CPU-only
            Seed = 0
        };

        _weights = LLamaWeights.LoadFromFile(_params);
        _ready = true;
        GD.Print($"LlmRunner: model loaded from {modelPath}");
    }

    /// <summary>
    /// Run inference with a system prompt and a user message.
    /// Returns the model's response as a string.
    /// </summary>
    public async Task<string> InferAsync(string systemPrompt, string userMessage,
        int maxTokens = 512, CancellationToken cancellationToken = default)
    {
        if (!_ready || _weights is null || _params is null)
            throw new InvalidOperationException("LlmRunner is not initialized");

        using var context = _weights.CreateContext(_params);
        var executor = new InstructExecutor(context);

        var inferenceParams = new InferenceParams
        {
            MaxTokens = maxTokens,
            AntiPrompts = new[] { "User:", "\n\n" }
        };

        // Build the full prompt: system + user
        var fullPrompt = $"{systemPrompt}\n\n{userMessage}";
        var result = new System.Text.StringBuilder();

        await foreach (var token in executor.InferAsync(fullPrompt, inferenceParams)
                           .WithCancellation(cancellationToken))
        {
            result.Append(token);
        }

        return result.ToString().Trim();
    }

    public void Dispose()
    {
        _weights?.Dispose();
        _weights = null;
        _ready = false;
    }
}
