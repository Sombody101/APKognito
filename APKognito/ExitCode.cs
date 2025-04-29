namespace APKognito;

enum ExitCode
{
    NoConsoleSession = -1,
    NoError = 0,
    InvalidCliUsage,
    InvalidInputArgument,
    ParentConsoleHandleNotFound = 5012,
    ConsoleCreationFailed,
}
