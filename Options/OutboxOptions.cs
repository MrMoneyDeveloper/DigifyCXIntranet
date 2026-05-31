namespace DigifyCXIntranet.Options;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Enabled { get; set; } = true;
    public string FolderPath { get; set; } = "App_Data\\Outbox";
    public string Path
    {
        get => FolderPath;
        set => FolderPath = value;
    }
}
