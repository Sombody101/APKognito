using System.IO;
using System.Text.RegularExpressions;

namespace APKognito.ApkMod.Automation;

public sealed partial class ConfigParser
{
    private const char STAGE_PREFIX = '@',
        VERSION_PREFIX = '#';

    private readonly StreamReader _stream;

    private readonly AutoConfigModel _configBuilder;

    private RenameStage _currentStage;

    private int lineIndex;

    public ConfigParser(StreamReader input)
    {
        _stream = input;
        _configBuilder = new();

        _currentStage = null!;
    }

    public async Task<AutoConfigModel> BeginParseAsync()
    {
        if (_stream.EndOfStream)
        {
            return _configBuilder;
        }

        await StartParseAsync();

        _configBuilder.Organize();

        return _configBuilder;
    }

    private async Task StartParseAsync()
    {
        string? line;

        var commentTrimmer = TrimCommentRegex();

        while ((line = await _stream.ReadLineAsync()) is not null)
        {
            lineIndex++;

            // Remove comments (the rest of the parser will assume as much, and will error out if a comment is found)
            line = commentTrimmer.Replace(line, string.Empty);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            ParseLine(line.Trim());
        }
    }

    private void ParseLine(string line)
    {
        switch (line[0])
        {
            case STAGE_PREFIX:
                CreateStage(line);
                break;

            case VERSION_PREFIX:

                break;

            default:
                // Assume it's a command
                CreateCommand(line);
                break;
        }
    }

    private void CreateStage(string section)
    {
        string formattedStage = section[1..].Trim();

        if (!Enum.TryParse(formattedStage, true, out CommandStage stage))
        {
            throw new UnknownStageException(formattedStage, lineIndex);
        }

        var existingStage = _configBuilder.Stages.Find(s => s.Stage == stage);

        if (existingStage is null)
        {
            _currentStage = new(stage);
            _configBuilder.Stages.Add(_currentStage);
        }
        else if (existingStage == _currentStage)
        {
            // Already editing that stage
            return;
        }
        else
        {
            _currentStage = existingStage;
        }
    }

    private void CreateCommand(string line)
    {
        if (_configBuilder.Stages.Count is 0)
        {
            throw new InvalidCommandOrderException($"Commands cannot precede stages. (line {lineIndex})");
        }

        var parsedCommand = SplitCommandRegex().Match(line);

        if (!parsedCommand.Success)
        {
            throw new InvalidCommandFormatException(lineIndex);
        }

        Command command = new(
            parsedCommand.Groups["command"].Value,
            [.. parsedCommand.Groups["args"].Captures.Select(c => c.Value)]
        );

        _currentStage.Commands.Add(command);
    }

    private void CollectVersion(string versionLine)
    {
        throw new NotImplementedException("Versions have not been implemented yet!s");

        if (_configBuilder.ConfigVersion is not null)
        {
            throw new InvalidVersionConfigException($"The configuration version can not be set more than once. (line {lineIndex})");
        }
    }

    [GeneratedRegex(@"\s*;.*")]
    private static partial Regex TrimCommentRegex();

    [GeneratedRegex(@"^\s*(?<command>\w+)(\s+(""(?<args>[^""]*)""|'(?<args>[^']*)'))*")]
    private static partial Regex SplitCommandRegex();

    public class InvalidCommandOrderException(string message) : Exception(message)
    {
    }

    public class UnknownStageException(string stageName, int line) : Exception($"Unknown stage '{stageName}' at line {line}.")
    {
    }

    public class InvalidCommandFormatException(int line) : Exception($"The line {line} is not in a valid format. (command 'argument' \"argument\" ...)")
    {
    }

    public class InvalidVersionConfigException(string message) : Exception(message)
    {
    }
}
