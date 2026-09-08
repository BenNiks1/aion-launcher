using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace AionLauncher.Core;

/// <summary>
/// Builds and validates the client command line. The client itself parses <c>-ip:</c>, <c>-port:</c>
/// and <c>-loginex</c> — that is the whole reason version.dll needs no change. Because these values
/// will come from a remote manifest in v1, the allowlist is enforced from day one.
/// </summary>
public static class LaunchArgs
{
    public const int DefaultLoginPort = 2106;
    public const string ClientExe64 = "bin64/aion.bin";
    public const string ClientExe32 = "bin32/aion.bin";

    private static readonly Regex HostnamePattern = new(
        @"^(?=.{1,253}$)[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Build(string host, int port)
    {
        if (!IsValidHost(host))
            throw new ArgumentException($"Некорректный адрес сервера: «{host}».", nameof(host));
        if (!IsValidPort(port))
            throw new ArgumentException($"Некорректный порт: {port}.", nameof(port));

        string[] args = [$"-ip:{host}", $"-port:{port.ToString(CultureInfo.InvariantCulture)}", "-loginex"];
        Validate(args);
        return args;
    }

    /// <summary>Throws on the first argument outside the allowlist, naming it.</summary>
    public static void Validate(IEnumerable<string> args)
    {
        foreach (string arg in args)
        {
            if (!IsAllowed(arg))
                throw new ArgumentException($"Аргумент запуска не разрешён: «{arg}».", nameof(args));
        }
    }

    public static bool IsAllowed(string? arg)
    {
        if (string.IsNullOrEmpty(arg))
            return false;

        if (arg == "-loginex")
            return true;

        if (arg.StartsWith("-ip:", StringComparison.Ordinal))
            return IsValidHost(arg["-ip:".Length..]);

        if (arg.StartsWith("-port:", StringComparison.Ordinal))
            return int.TryParse(arg["-port:".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int port)
                && IsValidPort(port);

        return false;
    }

    public static bool IsValidPort(int port) => port is >= 1 and <= 65535;

    /// <summary>IPv4 literal or DNS hostname. IPv6 is rejected: the 4.8 client cannot parse it in -ip:.</summary>
    public static bool IsValidHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253)
            return false;

        if (IPAddress.TryParse(host, out IPAddress? ip))
            return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

        return HostnamePattern.IsMatch(host);
    }

    /// <summary>Only the two real client executables may ever be started.</summary>
    public static bool IsAllowedClientExe(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        string normalized = SafePath.ToRelativeForm(relativePath);
        return normalized.Equals(ClientExe64, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(ClientExe32, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Absolute path of the client to start: 64-bit by default, 32-bit if asked or if 64-bit is absent.
    /// </summary>
    public static string ResolveClientExe(string gameRoot, bool prefer32Bit = false)
    {
        string[] order = prefer32Bit ? [ClientExe32, ClientExe64] : [ClientExe64, ClientExe32];

        foreach (string relative in order)
        {
            if (SafePath.TryResolveInside(gameRoot, relative, out string full) && File.Exists(full))
                return full;
        }

        throw new FileNotFoundException(
            $"В папке игры нет ни «{ClientExe64}», ни «{ClientExe32}».", Path.Combine(gameRoot, ClientExe64));
    }

    /// <summary>For the UI and the log line only — never fed back into a shell.</summary>
    public static string ToCommandLine(IEnumerable<string> args) => string.Join(' ', args);
}
