namespace ModSetup.Steps;

public class SetupStep
{
    public string? Content { get; set; }
    public string? ContentPath { get; set; }
    public string? Image { get; set; }
    public List<SetupAction>? Actions { get; set; }
    public bool SwitchStep { get; set; } = false;
    public List<SetupAction>? YesActions { get; set; }
    public List<SetupAction>? NoActions { get; set; }
    public bool Skippable { get; set; } = true;
}
