// See https://aka.ms/new-console-template for more information
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using DotnetEfMigrationsSquash;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal class Program
{
    private const string Format = "yyyyMMddHHmmss";
            
    private static DateTime _lastTimestamp = DateTime.MinValue;
    private static readonly object _lock = new();

    const string MigrationsFolderName = "Migrations";
    const string BackupFileNamePattern = "SquashedMigrationsBackup";

    private static async Task<int> Main(string[] args)
    {
        try
        {
            CommandOptions commandOptions = CreateCommandOptions();
            RootCommand rootCommand = CreateRootCommand(commandOptions);

            ParseResult parseResult = rootCommand.Parse(args);

            if (parseResult.Errors.Count != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                foreach (ParseError parseError in parseResult.Errors)
                {
                    Console.Error.WriteLine(parseError.Message);
                }
                return 1;
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = "dotnet-ef",
                Arguments = "--version",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var proc = Process.Start(startInfo);
            ArgumentNullException.ThrowIfNull(proc);
            string output = proc.StandardOutput.ReadToEnd();
            await proc.WaitForExitAsync();
            Console.WriteLine(output);

            if (proc.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("dotnet-ef command failed. Please ensure that the .NET SDK and the dotnet-ef tool are installed.");
                return 1;
            }

            string outputDirectory = (parseResult.GetValue(commandOptions.OutputDirOption) ?? parseResult.GetValue(commandOptions.ProjectOption)) ?? Directory.GetCurrentDirectory();

            string migrationsFolderPath = outputDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? outputDirectory += MigrationsFolderName
                : outputDirectory += Path.DirectorySeparatorChar + MigrationsFolderName;
            string backupFileName = GenerateId(BackupFileNamePattern);

            DirectoryInfo? di = new(migrationsFolderPath);

            string initialMigrationFileName = di.GetFiles()
                .Where(fi => !fi.Name.Contains("Designer", StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(fi => fi.Name).FirstOrDefault()?.Name ?? string.Empty;
            string initialMigrationDesignerFileName = di.GetFiles()
                .Where(fi => fi.Name.Contains("Designer", StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(fi => fi.Name).FirstOrDefault()?.Name ?? string.Empty;

            if (string.IsNullOrEmpty(initialMigrationFileName))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("No existing migrations found. Exiting.");
                return 1;
            }
            else
            {
                Console.WriteLine($"Found initial migration file: {initialMigrationFileName}");
                Console.WriteLine("Please confirm that this is correct before proceeding.");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Press Y to continue or any other key to exit.");
                var confirmInitialMigrationKey = Console.ReadKey();
                Console.WriteLine("");
                if (confirmInitialMigrationKey.Key != ConsoleKey.Y)
                {
                    Console.WriteLine("Exiting.");
                    return 0;
                }
            }

            Console.ResetColor();
            Console.WriteLine("Creating backup scripts for existing migrations.");

            startInfo = new()
            {
                FileName = "dotnet-ef",
                Arguments = $"dbcontext script {string.Join(' ', args)} --output {migrationsFolderPath}{Path.DirectorySeparatorChar}{backupFileName}.sql",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            proc = Process.Start(startInfo);
            ArgumentNullException.ThrowIfNull(proc);
            output = proc.StandardOutput.ReadToEnd();
            await proc.WaitForExitAsync();
            Console.WriteLine(output);

            if (proc.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("dotnet-ef dbcontext script failed.");
                return 1;
            }

            Console.WriteLine("Creating bundle self contained file for existing migrations.");

            startInfo = new()
            {
                FileName = "dotnet-ef",
                Arguments = $"migrations bundle {string.Join(' ', args)} --output {migrationsFolderPath}{Path.DirectorySeparatorChar}{backupFileName}.exe --self-contained -v",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            proc = Process.Start(startInfo);
            ArgumentNullException.ThrowIfNull(proc);
            output = proc.StandardOutput.ReadToEnd();
            await proc.WaitForExitAsync();
            Console.WriteLine(output);

            if (proc.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;                
                Console.Error.WriteLine("dotnet-ef migrations bundle failed.");
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("The floowing actions will remove all axisting migrations and will recreate the intial migration.");
            Console.WriteLine("Are you sure you want to continue?");
            Console.WriteLine("Press Y to continue or any other key to exit.");

            var confirmCleaningMigrationsKey = Console.ReadKey();
            Console.WriteLine("");
            if (confirmCleaningMigrationsKey.Key != ConsoleKey.Y)
            {
                Console.WriteLine("Exiting.");
                return 0;
            }            
            Console.ResetColor();

            Console.WriteLine("Cleaning up migration files.");

            di.GetFiles().OrderBy(fi => fi.CreationTimeUtc).ToList().ForEach(f =>
            {
                if (f.Name.Contains(backupFileName))
                {
                    Console.WriteLine($"Skipping generated backup file {f.Name}");
                }
                else
                {
                    Console.WriteLine($"Deleting file {f.Name}");
                    f.Delete();
                }
            });

            var match = Regex.Match(initialMigrationFileName, @"^[^_]+_(.+?)(?:\.(?:cs|vb))?$");
            string regeneratedInitialMigrationName = match.Groups[1].Value;

            startInfo = new()
            {
                FileName = "dotnet-ef",
                Arguments = $"migrations add {regeneratedInitialMigrationName} {string.Join(' ', args)}",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            proc = Process.Start(startInfo);
            ArgumentNullException.ThrowIfNull(proc);
            output = proc.StandardOutput.ReadToEnd();
            await proc.WaitForExitAsync();
            Console.WriteLine(output);

            if(proc.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("dotnet-ef migrations add failed.");
                return 1;
            }

            Console.WriteLine($"Recreating Intial Migration file by renaming '{regeneratedInitialMigrationName}' file that was generated with '{initialMigrationFileName}'.");

            di.GetFiles()
                .Where(fi => fi.Name.Contains(regeneratedInitialMigrationName, StringComparison.InvariantCultureIgnoreCase)).ToList()
                .ForEach(fi =>
                {
                    if (fi.FullName.Contains("Designer", StringComparison.InvariantCultureIgnoreCase))
                    {
                        fi.MoveTo($"{migrationsFolderPath}{Path.DirectorySeparatorChar}{initialMigrationDesignerFileName}");

                        var code = File.ReadAllText(fi.FullName);

                        var tree = CSharpSyntaxTree.ParseText(code);
                        var root = tree.GetRoot();

                        Console.WriteLine("Replacing the Migration attribute argument.");

                        var newRoot = root.ReplaceNodes(
                            root.DescendantNodes().OfType<AttributeSyntax>(),
                            (oldNode, _) =>
                            {
                                if (oldNode.Name.ToString() == "Migration")
                                {
                                    var newArg = SyntaxFactory.AttributeArgument(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            SyntaxFactory.Literal(Path.GetFileNameWithoutExtension(initialMigrationFileName))));

                                    return oldNode.WithArgumentList(
                                        SyntaxFactory.AttributeArgumentList(
                                            SyntaxFactory.SeparatedList([newArg])));
                                }

                                return oldNode;
                            });

                        File.WriteAllText(fi.FullName, newRoot.NormalizeWhitespace().ToFullString());
                    }
                    else
                    {
                        fi.MoveTo($"{migrationsFolderPath}{Path.DirectorySeparatorChar}{initialMigrationFileName}");
                    }
                });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Squash operation completed succesfully.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            Console.ResetColor();
        }
    }

    private static RootCommand CreateRootCommand(CommandOptions commandOptions)
    {
        RootCommand rootCommand = new("Sample app for System.CommandLine");
        
        rootCommand.Options.Add(commandOptions.JsonOption);
        rootCommand.Options.Add(commandOptions.ProjectOption);
        rootCommand.Options.Add(commandOptions.OutputDirOption);
        rootCommand.Options.Add(commandOptions.NamespaceOption);
        rootCommand.Options.Add(commandOptions.ContextOption);
        rootCommand.Options.Add(commandOptions.StartupProjectOption);
        rootCommand.Options.Add(commandOptions.FrameworkOption);
        rootCommand.Options.Add(commandOptions.ConfigurationOption);
        rootCommand.Options.Add(commandOptions.RuntimeOption);
        rootCommand.Options.Add(commandOptions.MsbuildProjectExtensionsPathOption);
        rootCommand.Options.Add(commandOptions.NoBuildOption);
        rootCommand.Options.Add(commandOptions.HelpOption);
        rootCommand.Options.Add(commandOptions.VerboseOption);
        rootCommand.Options.Add(commandOptions.NoColorOption);
        rootCommand.Options.Add(commandOptions.PrefixOutputOption);

        return rootCommand;
    }

    private static CommandOptions CreateCommandOptions()
    {
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Show JSON output. Use with --prefix-output to parse programatically."
        };
        var namespaceOption = new Option<string>("-n|--namespace", ["-n", "--namespace"])
        {
            Description = "The namespace to use. Matches the directory by default."
        };
        var contextOption = new Option<string>("-c|--context", ["-c", "--context"])
        {
            Description = "The DbContext to use. \"*\" can be used to run the command for all contexts found. This will also disable service discovery through the startup project if a corresponding IDesignTimeDbContextFactory implementation is found."
        };
        var startupProjectOption = new Option<string>("-s|--startup-project", ["-s", "--startup-project"])
        {
            Description = "The startup project to use. Defaults to the current working directory."
        };
        var frameworkOption = new Option<string>("--framework")
        {
            Description = "The target framework. Defaults to the first one in the project."
        };
        var configurationOption = new Option<string>("--configuration")
        {
            Description = "The configuration to use."
        };
        var runtimeOption = new Option<string>("--runtime")
        {
            Description = "The runtime to use."
        };
        var msbuildProjectExtensionsPathOption = new Option<string>("--msbuildprojectextensionspath")
        {
            Description = "The MSBuild project extensions path. Defaults to \"obj\"."
        };
        var noBuildOption = new Option<bool>("--no-build")
        {
            Description = "Don't build the project. Intended to be used when the build is up-to-date."
        };
        var helpOption = new Option<bool>("-h|--help", ["-h", "--help"])
        {
            Description = "Show help information."
        };
        var verboseOption = new Option<bool>("-v|--verbose", ["-v", "--verbose"])
        {
            Description = "Show verbose output."
        };
        var noColorOption = new Option<bool>("--no-color")
        {
            Description = "Don't colorize output."
        };
        var prefixOutputOption = new Option<bool>("--prefix-output")
        {
            Description = "Prefix output with level."
        };
        var projectOption = new Option<string>("-p", ["-p", "--project"])
        {
            Description = "The project to use. Defaults to the current working directory."
        };
        var outputDirOption = new Option<string>("-o|--output-dir", ["-o", "--output-dir"])
        {
            Description = "The directory to put files in. Paths are relative to the project directory. Defaults to \"Migrations\"."
        };

        return new CommandOptions(
            jsonOption,
            namespaceOption,
            contextOption,
            startupProjectOption,
            frameworkOption,
            configurationOption,
            runtimeOption,
            msbuildProjectExtensionsPathOption,
            noBuildOption,
            helpOption,
            verboseOption,
            noColorOption,
            prefixOutputOption,
            projectOption,
            outputDirOption
        );
    }

    public static string GenerateId(string name)
    {
        var now = DateTime.UtcNow;
        var timestamp = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

        lock (_lock)
        {
            if (timestamp <= _lastTimestamp)
            {
                timestamp = _lastTimestamp.AddSeconds(1);
            }

            _lastTimestamp = timestamp;
        }

        return timestamp.ToString(Format, CultureInfo.InvariantCulture) + "_" + name;
    }
}