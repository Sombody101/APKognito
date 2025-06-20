using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using APKognito.ApkLib.Exceptions;

namespace APKognito.ApkLib.Configuration;

public record BaseRenameConfiguration
{
    // Default regex: (?<=[./_])({value})(?=[./_])

    internal Regex? _builtRegexCache;

    internal string? InternalRenameRegexString { get; set; }

    internal int? InternalRenameRegexTimeoutMs { get; set; }

    internal string InternalRenameInfoLogDelimiter { get; set; } = null!;

    /// <summary>
    /// The regex to be used for renaming.
    /// </summary>
    public string? RenameRegex
    {
        get => InternalRenameRegexString;
        init => InternalRenameRegexString = value;
    }

    /// <summary>
    /// The number of milliseconds to wait before a regex operation times out. Defaults to 60,000 ms (60 seconds).
    /// </summary>
    public int? RegexTimeout
    {
        get => InternalRenameRegexTimeoutMs;
        init => InternalRenameRegexTimeoutMs = value;
    }

    /// <summary>
    /// A delimiter for package rename updates. Defaults to "<see langword=" to "/>"
    /// </summary>
    /// <example>
    /// com.me.myapp {<see cref="ReplacementInfoDelimiter"/>} com.newname.myapp
    /// </example>
    public string ReplacementInfoDelimiter
    {
        get => InternalRenameInfoLogDelimiter;
        init => InternalRenameInfoLogDelimiter = value;
    }

    internal BaseRenameConfiguration()
    {
    }

    /// <summary>
    /// Compiles the used <see cref="Regex"/> before use. This can help 
    /// </summary>
    /// <param name="originalCompanyName"></param>
    public void PrebuildRegex(string originalCompanyName)
    {
        BuildAndCacheRegex(originalCompanyName);
    }

    internal static T Coalesce<T>(T? overrideValue, T? configValue, [CallerArgumentExpression(nameof(configValue))] string? configName = null)
    {
        if (overrideValue is not null)
        {
            return overrideValue;
        }

        return configValue is null
            ? throw new InvalidConfigurationException($"{configName} of type {typeof(T).Name} was null, and no override value was supplied.")
            : configValue;
    }

    internal static T Coalesce<T>(T? overrideValue, Func<T> resolveValue, [CallerArgumentExpression(nameof(overrideValue))] string? configName = null)
    {
        if (overrideValue is not null)
        {
            return overrideValue;
        }

        return resolveValue() ?? throw new InvalidConfigurationException($"{configName} of type {typeof(T).Name} was null, and no override value was supplied.");
    }

    internal static T CoalesceConfigurations<T>(T? overrideValue, T? configValue, T defaultValue)
    {
        if (overrideValue is not null)
        {
            return overrideValue;
        }

        return configValue is not null
            ? configValue
            : defaultValue;
    }

    internal static T CoalesceConfigurations<T>(T? overrideValue, Func<T> resolveValue, T defaultValue)
    {
        if (overrideValue is not null)
        {
            return overrideValue;
        }

        T? resolvedValue = resolveValue();
        return resolvedValue is not null
            ? resolvedValue
            : defaultValue;
    }

    internal Regex BuildAndCacheRegex(string originalCompanyName, int regexTimeoutMs = 60_000, bool forceBuild = false)
    {
        if (_builtRegexCache is not null && !forceBuild)
        {
            return _builtRegexCache;
        }

        ArgumentNullException.ThrowIfNull(RenameRegex);

        string pattern = RenameRegex.Replace("{value}", originalCompanyName);

        _builtRegexCache = new Regex(pattern,
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(regexTimeoutMs)
        );

        return _builtRegexCache;
    }

    internal void ApplyOverrides(BaseRenameConfiguration global)
    {
        InternalRenameRegexString ??= global.InternalRenameRegexString;
        InternalRenameRegexTimeoutMs ??= global.InternalRenameRegexTimeoutMs;
        InternalRenameInfoLogDelimiter ??= global.InternalRenameInfoLogDelimiter;

        // This *might* help with reducing regex build count. Only works if
        // I'm building the regex from the global config somewhere in the PackageEditorContext.
        _builtRegexCache ??= global._builtRegexCache;
    }
}
