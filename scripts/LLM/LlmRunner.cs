using LLama;
using LLama.Common;
using LLama.Sampling;
using Godot;

namespace AinSoph.LLM;

/// <summary>
/// Manages a single loaded Qwen 2.5 3B model instance.
/// Used by NPCs and the Triune Council.
/// CPU-only. Low-spec baseline: 8GB RAM.
///
/// Call Initialize() once at startup with the path to the .gguf model file.
/// Then call InferAsync() to get a completion for a given system prompt + user prompt.
///
/// If no model is loaded the runner is in demo mode: InferAsync answers with
/// scripted replies from DemoResponder so the game is still playable.
/// </summary>
public class LlmRunner : IDisposable
{
    private LLamaWeights? _weights;
    private ModelParams? _params;
    private bool _ready;

    // llama.cpp contexts are heavy; run one inference at a time
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsReady => _ready;
    public bool IsDemo  => !_ready;

    /// <summary>
    /// Load the model from a .gguf file. An empty path leaves the runner in demo mode.
    /// modelPath: path to the Qwen 2.5 3B .gguf file.
    /// contextSize: token context window. 2048 is sufficient for NPC and Council use.
    /// </summary>
    public void Initialize(string modelPath, uint contextSize = 2048)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            GD.Print("LlmRunner: no model — demo mode (scripted responses)");
            return;
        }

        if (!System.IO.File.Exists(modelPath))
        {
            GD.PrintErr($"LlmRunner: model file not found at {modelPath} — demo mode");
            return;
        }

        _params = new ModelParams(modelPath)
        {
            ContextSize = contextSize,
            GpuLayerCount = 0,   // CPU-only
            // One sequence per context. LLamaSharp's default splits the context
            // across many sequences, leaving ~32 tokens each — every prompt then
            // fails with llama_decode 'NoKvSlot'.
            SeqMax = 1,
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
            return await DemoResponder.RespondAsync(systemPrompt, userMessage, cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Stateless: each call gets a fresh context, so NPCs never see each other's prompts
            var executor = new StatelessExecutor(_weights, _params);

            var inferenceParams = new InferenceParams
            {
                MaxTokens = maxTokens,
                AntiPrompts = new[] { "<|im_end|>", "<|im_start|>" },
                // Small models loop ("I will not… I will not…") until they hit MaxTokens
                SamplingPipeline = new DefaultSamplingPipeline { RepeatPenalty = 1.15f, Temperature = 0.7f },
            };

            // Qwen 2.5 is trained on ChatML
            var fullPrompt =
                $"<|im_start|>system\n{systemPrompt}<|im_end|>\n" +
                $"<|im_start|>user\n{userMessage}<|im_end|>\n" +
                "<|im_start|>assistant\n";

            var result = new System.Text.StringBuilder();

            await foreach (var token in executor.InferAsync(fullPrompt, inferenceParams, cancellationToken))
                result.Append(token);

            return result.ToString()
                .Replace("<|im_end|>", "")
                .Replace("<|im_start|>", "")
                .Trim();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Pull the first top-level JSON object out of a model reply.
    /// Small models often wrap JSON in prose or markdown fences.
    /// </summary>
    public static string ExtractJson(string raw) =>
        ExtractJsonObjects(raw).FirstOrDefault() ?? raw.Trim();

    /// <summary>Every top-level JSON object in a model reply, in order.</summary>
    public static IEnumerable<string> ExtractJsonObjects(string raw)
    {
        int from = 0;
        while (from < raw.Length)
        {
            var obj = ExtractJsonFrom(raw, from, out var end);
            if (obj is null) yield break;
            yield return obj;
            from = end;
        }
    }

    private static string? ExtractJsonFrom(string raw, int from, out int end)
    {
        end = raw.Length;
        var start = raw.IndexOf('{', from);
        if (start < 0) return null;

        int depth = 0;
        bool inString = false, escaped = false;
        for (int i = start; i < raw.Length; i++)
        {
            var c = raw[i];
            if (escaped)          { escaped = false; continue; }
            if (c == '\\')        { escaped = true;  continue; }
            if (c == '"')         { inString = !inString; continue; }
            if (inString)         continue;
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0)
            {
                end = i + 1;
                return raw[start..end];
            }
        }
        return raw[start..].Trim();
    }

    public void Dispose()
    {
        _weights?.Dispose();
        _weights = null;
        _ready = false;
    }
}
