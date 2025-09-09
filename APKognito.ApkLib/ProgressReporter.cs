﻿namespace APKognito.ApkLib;

internal static class ProgressReporter
{
    public static void ReportProgress(this IProgress<ProgressInfo> reporter, string data, ProgressUpdateType updateType)
    {
        reporter.Report(new(data, updateType));
    }

    public static void ReportProgressTitle(this IProgress<ProgressInfo>? reporter, params string[] title)
    {
        if (reporter is null)
        {
            return;
        }

        string formattedTitle = string.Concat(title);

        ArgumentException.ThrowIfNullOrEmpty(formattedTitle);

        ReportProgress(reporter, formattedTitle, ProgressUpdateType.Title);
    }

    public static void ReportProgressMessage(this IProgress<ProgressInfo>? reporter, params string[] message)
    {
        if (reporter is null)
        {
            return;
        }

        string formattedMessage = string.Concat(message);

        ArgumentException.ThrowIfNullOrEmpty(formattedMessage);

        ReportProgress(reporter, formattedMessage, ProgressUpdateType.Content);
    }

    public static void Clear(this IProgress<ProgressInfo>? reporter)
    {
        reporter?.Report(new(string.Empty, ProgressUpdateType.Reset));
    }
}
