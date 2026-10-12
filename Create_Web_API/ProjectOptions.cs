namespace Create_Web_API;

public enum UiChoice
{
    None,
    Avalonia,
    Wpf
}

public enum DatabaseChoice
{
    None,
    CreateNewSqlite,
    ConnectExisting
}

public class ProjectOptions
{
    public string TargetDirectory { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public UiChoice Ui { get; set; } = UiChoice.None;
    public DatabaseChoice Database { get; set; } = DatabaseChoice.None;
    public string ExistingDbPath { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}
