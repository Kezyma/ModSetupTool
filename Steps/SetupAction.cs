using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ModSetup.Steps;

public class SetupAction
{
    [JsonConverter(typeof(StringEnumConverter))]
    public StepType StepType { get; set; }

    public string? AppPath { get; set; }
    public string? AppArgs { get; set; }

    public int? StepIndex { get; set; }

    public Dictionary<string, string>? FileMaps { get; set; }
    public string[]? FilePaths { get; set; }

    public bool Wait { get; set; } = true;
    public int? Delay { get; set; }
}
