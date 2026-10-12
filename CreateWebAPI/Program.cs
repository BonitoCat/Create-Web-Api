using Spectre.Console;

namespace Create_Web_API;

class Program
{
    static void Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("WebAPI Creator")
                .Color(Color.DeepSkyBlue1));

        AnsiConsole.MarkupLine("[bold grey]Interactive scaffolding tool for .NET Web API solutions[/]\n");

        string defaultPath = Directory.GetCurrentDirectory();
        string targetDirectory = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter target directory path:")
                .DefaultValue(defaultPath)
                .Validate(path =>
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return ValidationResult.Error("Target path cannot be empty.");
                    }
                    
                    return ValidationResult.Success();
                }));

        targetDirectory = PathHelper.ResolvePath(targetDirectory);

        string projectName = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter project/solution name:")
                .DefaultValue("MyWebApi")
                .Validate(name =>
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        return ValidationResult.Error("Project name cannot be empty.");
                    }
                    
                    if (name.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
                    {
                        return ValidationResult.Error("Project name must contain only letters, numbers, or underscores.");
                    }
                    
                    return ValidationResult.Success();
                }));

        string uiChoiceString = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose UI client:")
                .AddChoices(new string[]
                {
                    "1. No UI",
                    "2. Avalonia UI (Cross-platform)",
                    "3. WPF (Windows only)"
                }));

        UiChoice uiChoice = uiChoiceString switch
        {
            "2. Avalonia UI (Cross-platform)" => UiChoice.Avalonia,
            "3. WPF (Windows only)" => UiChoice.Wpf,
            _ => UiChoice.None
        };

        string dbChoiceString = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose Database configuration:")
                .AddChoices(new string[]
                {
                    "1. None",
                    "2. Create new SQLite database (Code-First)",
                    "3. Connect to existing database (.sqlite, .db, .mdf)"
                }));

        DatabaseChoice dbChoice = dbChoiceString switch
        {
            "2. Create new SQLite database (Code-First)" => DatabaseChoice.CreateNewSqlite,
            "3. Connect to existing database (.sqlite, .db, .mdf)" => DatabaseChoice.ConnectExisting,
            _ => DatabaseChoice.None
        };

        string existingDbPath = string.Empty;
        string tableName = string.Empty;

        if (dbChoice == DatabaseChoice.ConnectExisting)
        {
            existingDbPath = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter path to database file:")
                    .Validate(path =>
                    {
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            return ValidationResult.Error("Path cannot be empty.");
                        }

                        string resolved = PathHelper.ResolvePath(path);
                        if (!File.Exists(resolved))
                        {
                            return ValidationResult.Error($"File not found: {resolved}");
                        }

                        return ValidationResult.Success();
                    }));

            existingDbPath = PathHelper.ResolvePath(existingDbPath);

            tableName = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter primary table name to query (or press Enter for default):")
                    .AllowEmpty());
        }

        ProjectOptions options = new()
        {
            TargetDirectory = targetDirectory,
            ProjectName = projectName,
            Ui = uiChoice,
            Database = dbChoice,
            ExistingDbPath = existingDbPath,
            TableName = tableName
        };

        Table summaryTable = new();
        summaryTable.AddColumn("Option");
        summaryTable.AddColumn("Value");
        summaryTable.AddRow("Target Directory", $"[cyan]{Markup.Escape(options.TargetDirectory)}[/]");
        summaryTable.AddRow("Project Name", $"[green]{Markup.Escape(options.ProjectName)}[/]");
        summaryTable.AddRow("UI Client", $"[yellow]{options.Ui}[/]");
        summaryTable.AddRow("Database", $"[magenta]{options.Database}[/]");

        if (options.Database == DatabaseChoice.ConnectExisting)
        {
            summaryTable.AddRow("Database Path", $"[grey]{Markup.Escape(options.ExistingDbPath)}[/]");
            summaryTable.AddRow("Table Name", $"[grey]{Markup.Escape(options.TableName)}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        bool confirm = AnsiConsole.Confirm("Proceed with creating this project?", true);
        if (!confirm)
        {
            AnsiConsole.MarkupLine("[red]Operation canceled.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        ProjectGenerator.Generate(options);
    }
}
