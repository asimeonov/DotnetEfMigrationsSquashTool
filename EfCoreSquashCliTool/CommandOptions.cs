using System.CommandLine;

namespace EfCoreSqwashCliTool;

public record CommandOptions(
    Option<bool> JsonOption,
    Option<string> NamespaceOption,
    Option<string> ContextOption,
    Option<string> StartupProjectOption,
    Option<string> FrameworkOption,
    Option<string> ConfigurationOption,
    Option<string> RuntimeOption,
    Option<string> MsbuildProjectExtensionsPathOption,
    Option<bool> NoBuildOption,
    Option<bool> HelpOption,
    Option<bool> VerboseOption,
    Option<bool> NoColorOption,
    Option<bool> PrefixOutputOption,
    Option<string> ProjectOption,
    Option<string> OutputDirOption
);
