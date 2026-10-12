using Spectre.Console;

namespace Create_Web_API;

public static class ProjectGenerator
{
    public static void Generate(ProjectOptions options)
    {
        string fullTargetPath = PathHelper.ResolvePath(options.TargetDirectory);
        string solutionDir = Path.Combine(fullTargetPath, options.ProjectName);
        if (!Directory.Exists(solutionDir))
        {
            Directory.CreateDirectory(solutionDir);
        }

        string slnName = options.ProjectName;
        string apiProjectName = options.ProjectName;
        string apiProjectPath = Path.Combine(solutionDir, apiProjectName);

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Generating solution and Web API project...", (StatusContext ctx) =>
            {
                ctx.Status("Creating solution...");
                ProcessRunner.Run("dotnet", $"new sln -n \"{slnName}\"", solutionDir);

                ctx.Status("Creating Web API project...");
                Directory.CreateDirectory(apiProjectPath);
                ProcessRunner.Run("dotnet", "new web", apiProjectPath);

                ctx.Status("Adding Web API project to solution...");
                ProcessRunner.Run("dotnet", $"sln add \"{apiProjectName}\"", solutionDir);

                ctx.Status("Installing NuGet packages in Web API project...");
                InstallApiPackages(apiProjectPath);

                string dbProjectName = $"{options.ProjectName}Db";
                string dbProjectPath = Path.Combine(solutionDir, dbProjectName);

                if (options.Database != DatabaseChoice.None)
                {
                    ctx.Status("Creating EF Core Database library project...");
                    Directory.CreateDirectory(dbProjectPath);
                    ProcessRunner.Run("dotnet", "new classlib", dbProjectPath);

                    string class1Path = Path.Combine(dbProjectPath, "Class1.cs");
                    if (File.Exists(class1Path))
                    {
                        File.Delete(class1Path);
                    }

                    ProcessRunner.Run("dotnet", $"sln add \"{dbProjectName}\"", solutionDir);
                    ProcessRunner.Run("dotnet", $"add \"{apiProjectPath}\" reference \"{dbProjectPath}\"", solutionDir);

                    ctx.Status("Installing EF Core packages in Database library...");
                    InstallDatabasePackages(dbProjectPath, options);

                    if (options.Database == DatabaseChoice.ConnectExisting)
                    {
                        ctx.Status("Scaffolding DbContext from existing database...");
                        ScaffoldExistingDb(dbProjectPath, options);
                    }
                    else
                    {
                        ctx.Status("Configuring Code-First SQLite DbContext...");
                        ConfigureCodeFirstDb(dbProjectPath, options);
                    }
                }

                ctx.Status("Creating project folders and templates...");
                ConfigureApiProjectFiles(apiProjectPath, options, dbProjectName);

                if (options.Ui == UiChoice.Avalonia)
                {
                    ctx.Status("Creating Avalonia UI project...");
                    CreateAvaloniaUi(solutionDir, options);
                }
                else if (options.Ui == UiChoice.Wpf)
                {
                    ctx.Status("Creating WPF UI project...");
                    CreateWpfUi(solutionDir, options);
                }
            });

        AnsiConsole.MarkupLine("[green]✔ Projects created and configured successfully![/]");
    }

    private static void InstallApiPackages(string apiPath)
    {
        ProcessRunner.Run("dotnet", "add package Swashbuckle.AspNetCore", apiPath);
        ProcessRunner.Run("dotnet", "add package Microsoft.AspNetCore.OpenApi", apiPath);
        ProcessRunner.Run("dotnet", "add package Microsoft.EntityFrameworkCore.Design", apiPath);
    }

    private static void InstallDatabasePackages(string dbPath, ProjectOptions options)
    {
        ProcessRunner.Run("dotnet", "add package Microsoft.EntityFrameworkCore", dbPath);
        ProcessRunner.Run("dotnet", "add package Microsoft.EntityFrameworkCore.Design", dbPath);

        bool isSqlServer = options.Database == DatabaseChoice.ConnectExisting &&
            (options.ExistingDbPath.EndsWith(".mdf", StringComparison.OrdinalIgnoreCase) ||
             options.ExistingDbPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));

        if (isSqlServer)
        {
            ProcessRunner.Run("dotnet", "add package Microsoft.EntityFrameworkCore.SqlServer", dbPath);
            return;
        }

        ProcessRunner.Run("dotnet", "add package Microsoft.EntityFrameworkCore.Sqlite", dbPath);
    }

    private static void ScaffoldExistingDb(string dbPath, ProjectOptions options)
    {
        string dbFile = PathHelper.ResolvePath(options.ExistingDbPath);
        string dbName = Path.GetFileNameWithoutExtension(dbFile);
        bool isSqlServer = dbFile.EndsWith(".mdf", StringComparison.OrdinalIgnoreCase);

        string provider = isSqlServer ? "Microsoft.EntityFrameworkCore.SqlServer" : "Microsoft.EntityFrameworkCore.Sqlite";
        string connStr = isSqlServer
            ? $"Server=(LocalDB)\\mssqllocaldb;attachdbfilename={dbFile};integrated security=True"
            : $"data source={dbFile}";

        ProcessRunner.Run("dotnet", $"ef dbcontext scaffold \"{connStr}\" {provider} -f -c \"{dbName}Context\" --no-onconfiguring", dbPath);
    }

    private static void ConfigureCodeFirstDb(string dbPath, ProjectOptions options)
    {
        string dummyModelContent =
$@"namespace {options.ProjectName}Db;

public class Dummy
{{
    public int Id {{ get; set; }}
    public string Name {{ get; set; }} = string.Empty;
}}
";
        File.WriteAllText(Path.Combine(dbPath, "Dummy.cs"), dummyModelContent);

        string dbContextContent =
$@"using Microsoft.EntityFrameworkCore;

namespace {options.ProjectName}Db;

public class AppDbContext : DbContext
{{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {{
    }}

    public DbSet<Dummy> Dummies => Set<Dummy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {{
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Dummy>().HasData(
            new Dummy {{ Id = 1, Name = ""Dummy Item 1"" }},
            new Dummy {{ Id = 2, Name = ""Dummy Item 2"" }}
        );
    }}
}}
";
        File.WriteAllText(Path.Combine(dbPath, "AppDbContext.cs"), dbContextContent);
    }

    private static void ConfigureApiProjectFiles(string apiPath, ProjectOptions options, string dbProjectName)
    {
        Directory.CreateDirectory(Path.Combine(apiPath, "Dtos"));
        Directory.CreateDirectory(Path.Combine(apiPath, "Services"));
        Directory.CreateDirectory(Path.Combine(apiPath, "Controllers"));

        string resolvedExistingDb = PathHelper.ResolvePath(options.ExistingDbPath);

        string appsettingsJson = options.Database switch
        {
            DatabaseChoice.ConnectExisting when resolvedExistingDb.EndsWith(".mdf", StringComparison.OrdinalIgnoreCase) =>
$@"{{
  ""Logging"": {{
    ""LogLevel"": {{
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }}
  }},
  ""AllowedHosts"": ""*"",
  ""ConnectionStrings"": {{
    ""DefaultConnection"": ""Server=(LocalDB)\\mssqllocaldb;attachdbfilename={resolvedExistingDb.Replace("\\", "\\\\")};integrated security=True""
  }}
}}",
            DatabaseChoice.ConnectExisting =>
$@"{{
  ""Logging"": {{
    ""LogLevel"": {{
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }}
  }},
  ""AllowedHosts"": ""*"",
  ""ConnectionStrings"": {{
    ""DefaultConnection"": ""Data Source={resolvedExistingDb.Replace("\\", "\\\\")}""
  }}
}}",
            DatabaseChoice.CreateNewSqlite =>
$@"{{
  ""Logging"": {{
    ""LogLevel"": {{
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }}
  }},
  ""AllowedHosts"": ""*"",
  ""ConnectionStrings"": {{
    ""DefaultConnection"": ""Data Source=app.db""
  }}
}}",
            _ =>
@"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*""
}"
        };

        File.WriteAllText(Path.Combine(apiPath, "appsettings.json"), appsettingsJson);

        string dbContextName = options.Database switch
        {
            DatabaseChoice.ConnectExisting => $"{Path.GetFileNameWithoutExtension(options.ExistingDbPath)}Context",
            DatabaseChoice.CreateNewSqlite => "AppDbContext",
            _ => string.Empty
        };

        bool isSqlServer = options.Database == DatabaseChoice.ConnectExisting &&
                           options.ExistingDbPath.EndsWith(".mdf", StringComparison.OrdinalIgnoreCase);

        string dbRegistration = options.Database switch
        {
            DatabaseChoice.None => string.Empty,
            _ when isSqlServer => $"builder.Services.AddDbContext<{dbContextName}>(options => options.UseSqlServer(builder.Configuration.GetConnectionString(\"DefaultConnection\")));",
            _ => $"builder.Services.AddDbContext<{dbContextName}>(options => options.UseSqlite(builder.Configuration.GetConnectionString(\"DefaultConnection\")));"
        };

        string dbUsings = options.Database != DatabaseChoice.None
            ? $"using Microsoft.EntityFrameworkCore;\nusing {dbProjectName};\n"
            : string.Empty;

        string ensureDbCreation = options.Database == DatabaseChoice.CreateNewSqlite
            ? $@"using (IServiceScope scope = app.Services.CreateScope())
{{
    {dbContextName} db = scope.ServiceProvider.GetRequiredService<{dbContextName}>();
    await db.Database.EnsureCreatedAsync();
}}
"
            : string.Empty;

        string programContent =
$@"{dbUsings}using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{{
    options.SwaggerDoc(""v1"", new OpenApiInfo {{ Title = ""{options.ProjectName}"", Version = ""v1"" }});
}});

builder.Services.AddCors(options =>
{{
    options.AddPolicy(""AllowAll"", policy =>
    {{
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }});
}});

{dbRegistration}

WebApplication app = builder.Build();

{ensureDbCreation}
if (app.Environment.IsDevelopment())
{{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint(""/swagger/v1/swagger.json"", ""{options.ProjectName} v1""));
}}

app.UseCors(""AllowAll"");
app.UseAuthorization();

app.MapControllers();
app.MapGet(""/"", () => Results.Redirect(""/swagger""));

app.Run();
";

        File.WriteAllText(Path.Combine(apiPath, "Program.cs"), programContent);

        string controllerContent = options.Database == DatabaseChoice.None
            ? GetNoDbController(options.ProjectName)
            : GetDbController(options.ProjectName, dbProjectName, dbContextName, options.TableName);

        File.WriteAllText(Path.Combine(apiPath, "Controllers", "ValuesController.cs"), controllerContent);
    }

    private static string GetNoDbController(string projectName)
    {
        return
$@"using Microsoft.AspNetCore.Mvc;

namespace {projectName}.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class ValuesController : ControllerBase
{{
    [HttpGet]
    public ActionResult<IEnumerable<string>> Get()
    {{
        return Ok(new string[] {{ ""value1"", ""value2"" }});
    }}
}}
";
    }

    private static string GetDbController(string projectName, string dbProjectName, string dbContextName, string tableName)
    {
        string target = string.IsNullOrWhiteSpace(tableName) ? "Dummies" : tableName;

        return
$@"using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using {dbProjectName};

namespace {projectName}.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class ValuesController : ControllerBase
{{
    private readonly {dbContextName} _context;

    public ValuesController({dbContextName} context)
    {{
        _context = context;
    }}

    [HttpGet]
    public async Task<IActionResult> Get()
    {{
        return Ok(await _context.{target}.Take(100).ToListAsync());
    }}
}}
";
    }

    private static void CreateAvaloniaUi(string solutionDir, ProjectOptions options)
    {
        string uiProjectName = $"{options.ProjectName}.Desktop";
        string uiPath = Path.Combine(solutionDir, uiProjectName);
        Directory.CreateDirectory(uiPath);

        ProcessRunner.Run("dotnet", "new avalonia.app --no-restore", uiPath);
        ProcessRunner.Run("dotnet", $"sln add \"{uiProjectName}\"", solutionDir);
        ProcessRunner.Run("dotnet", "restore", uiPath);
    }

    private static void CreateWpfUi(string solutionDir, ProjectOptions options)
    {
        string uiProjectName = $"{options.ProjectName}.Wpf";
        string uiPath = Path.Combine(solutionDir, uiProjectName);
        Directory.CreateDirectory(uiPath);

        ProcessRunner.Run("dotnet", "new wpf --no-restore", uiPath);
        ProcessRunner.Run("dotnet", $"sln add \"{uiProjectName}\"", solutionDir);
    }
}
