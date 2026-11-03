using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Coiny.API.Logging;

public sealed class JournalOptions
{
    public string Directory { get; set; } = "";
    public int RetentionDays { get; set; } = 7;
    public int FileSizeMb { get; set; } = 4;
    public int MaxFiles { get; set; } = 8;
}
public sealed record JournalEntry(string Id, DateTimeOffset Timestamp, string Source, string Level,
    string Message, string? RequestId, IReadOnlyDictionary<string, string> Fields);

// The journal accepts only named events and allowlisted fields, never arbitrary
// formatted messages, exception messages, request bodies, headers, or SQL.
public sealed class OperationalJournal : BackgroundService
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Channel<JournalEntry> queue = Channel.CreateBounded<JournalEntry>(new BoundedChannelOptions(2048) {
        FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
    private readonly ConcurrentQueue<JournalEntry> recent = new();
    private readonly JournalOptions options;
    private readonly TaskCompletionSource executing = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long dropped, writeFailures;
    private string? file;
    private long fileBytes;
    public long Dropped => Interlocked.Read(ref dropped);
    public long WriteFailures => Interlocked.Read(ref writeFailures);
    public bool Persistent => !string.IsNullOrWhiteSpace(options.Directory);
    public const int RecentLimit = 5000;
    private static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<string, string> {
        ["request.completed"] = "HTTP request completed", ["request.failed"] = "HTTP request failed",
        ["application.warning"] = "Application warning", ["application.error"] = "Application error",
        ["email.failed"] = "Account email delivery failed", ["storage.failed"] = "Household storage request failed",
        ["job.failed"] = "Recurring automation failed; will retry",
        ["application.started"] = "Coiny logging started"
    };
    private static readonly HashSet<string> Fields = ["method", "route", "status", "durationMs", "exceptionType", "category", "eventId"];
    public OperationalJournal(JournalOptions options) { this.options = options; }
    public void Record(string source, string level, string name, string? requestId = null, params (string Key, string Value)[] fields)
    {
        if (!Messages.TryGetValue(name, out var message)) return;
        if (source is not ("api" or "jobs")) return;
        var safe = fields.Where(f => Fields.Contains(f.Key)).ToDictionary(f => f.Key, f => Safe(f.Value));
        safe["event"] = name;
        var entry = new JournalEntry(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, source, level, message, requestId, safe);
        if (!queue.Writer.TryWrite(entry)) Interlocked.Increment(ref dropped);
    }
    private static string Safe(string value) => new(value.Take(200).Where(c => !char.IsControl(c)).ToArray());
    public JournalEntry[] Snapshot() => recent.Where(e => e.Timestamp >= DateTimeOffset.UtcNow.AddDays(-Math.Clamp(options.RetentionDays, 1, 30))).ToArray();
    private void Remember(JournalEntry entry) { recent.Enqueue(entry); while (recent.Count > RecentLimit) recent.TryDequeue(out _); }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        executing.TrySetResult();
        if (Persistent) {
            try {
                System.IO.Directory.CreateDirectory(options.Directory);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(options.Directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                foreach (var path in Files().OrderBy(p => p, StringComparer.Ordinal))
                    foreach (var line in File.ReadLines(path)) {
                        try {
                            var entry = JsonSerializer.Deserialize<JournalEntry>(line, Json);
                            if (entry == null) continue;
                            Remember(entry);
                        }
                        catch (JsonException) { /* Ignore incomplete JSON after an abrupt shutdown. */ }
                    }
                Prune();
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Failed(); }
        }
        Record("api", "Information", "application.started");
        // StopAsync completes the writer first; drain accepted events on graceful shutdown.
        long reportedDrops = 0; var nextDropNotice = DateTimeOffset.MinValue;
        await foreach (var entry in queue.Reader.ReadAllAsync()) {
            if (Dropped > reportedDrops && DateTimeOffset.UtcNow >= nextDropNotice) {
                reportedDrops = Dropped; nextDropNotice = DateTimeOffset.UtcNow.AddSeconds(30);
                Console.Error.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, source = "api", level = "Warning",
                    message = "Log queue capacity exceeded; events dropped", dropped = reportedDrops }, Json));
            }
            Remember(entry);
            var line = JsonSerializer.Serialize(entry, Json);
            Console.WriteLine(line);
            if (!Persistent) continue;
            try {
                var bytes = Encoding.UTF8.GetBytes(line + "\n");
                if (file == null || fileBytes + bytes.Length > Math.Clamp(options.FileSizeMb, 1, 16) * 1024L * 1024 || !File.Exists(file)) {
                    file = Path.Combine(options.Directory, $"coiny-{DateTime.UtcNow:yyyyMMddTHHmmssfffffff}-{Guid.NewGuid():N}.jsonl"); fileBytes = 0;
                    System.IO.Directory.CreateDirectory(options.Directory);
                    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(options.Directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    var createOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                    if (!OperatingSystem.IsWindows()) createOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    using var created = new FileStream(file, createOptions);
                }
                await using (var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read)) await stream.WriteAsync(bytes);
                fileBytes += bytes.Length; Prune();
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { file = null; Failed(); }
        }
    }
    private void Failed() {
        if (Interlocked.Increment(ref writeFailures) == 1 || WriteFailures % 100 == 0)
            Console.Error.WriteLine("{\"source\":\"api\",\"level\":\"Error\",\"message\":\"Log journal storage unavailable; console collection continues\"}");
    }
    private string[] Files() => System.IO.Directory.GetFiles(options.Directory, "coiny-*.jsonl");
    private void Prune() {
        var paths = Files().OrderByDescending(p => p, StringComparer.Ordinal).ToArray();
        for (var i = 0; i < paths.Length; i++)
            if (paths[i] != file && (i >= Math.Clamp(options.MaxFiles, 2, 32) || File.GetLastWriteTimeUtc(paths[i]) < DateTime.UtcNow.AddDays(-Math.Clamp(options.RetentionDays, 1, 30)))) File.Delete(paths[i]);
    }
    public override async Task StopAsync(CancellationToken cancellationToken) {
        queue.Writer.TryComplete();
        // .NET schedules ExecuteAsync on a background task. Do not cancel that
        // task before it starts: already accepted events still need draining.
        if (ExecuteTask is { } task) await Task.WhenAny(executing.Task, task).WaitAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}

public sealed class SafeJournalProvider(OperationalJournal journal) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SafeLogger(journal, categoryName);
    public void Dispose() { }
    private sealed class SafeLogger(OperationalJournal journal, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning && level < LogLevel.None;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (!IsEnabled(level)) return;
            // Deliberately do not invoke formatter or serialize state/exception text.
            var name = level >= LogLevel.Error ? "application.error" : "application.warning";
            var failureType = exception?.GetType().Name ?? "none";
            if (state is IEnumerable<KeyValuePair<string, object?>> values) {
                var template = values.FirstOrDefault(p => p.Key == "{OriginalFormat}").Value as string;
                name = template switch {
                    "Account email delivery failed ({FailureType}). Check the mail service configuration." or
                    "Password recovery email delivery failed ({FailureType}). Check the mail service configuration." => "email.failed",
                    "Storage request failed ({FailureType})." => "storage.failed",
                    "Recurring scan failed for household {HouseholdId}; will retry." or
                    "Recurring scan scheduler could not check households; will retry." => "job.failed",
                    _ => name
                };
                if (values.FirstOrDefault(p => p.Key == "FailureType").Value is string type && type.Length <= 100 &&
                    type.EndsWith("Exception", StringComparison.Ordinal) && type.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) failureType = type;
            }
            journal.Record(category.Contains("Recurring", StringComparison.Ordinal) ? "jobs" : "api", level.ToString(),
                name, Activity.Current?.TraceId.ToString(),
                ("category", category), ("eventId", eventId.Id.ToString()), ("exceptionType", failureType));
        }
    }
}
