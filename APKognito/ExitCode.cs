namespace APKognito;

enum ExitCode
{
    NoConsoleSession = -1,
    NoError = 0,
    InvalidCliUsage,
    InvalidInputArgument,
    InvalidAnchorConfiguration,
    ParentConsoleHandleNotFound = 5012,
    ConsoleCreationFailed,
}
