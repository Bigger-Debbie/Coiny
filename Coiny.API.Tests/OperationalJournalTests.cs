using System.Text.Json;
using Coiny.API.Logging;
using Microsoft.Extensions.Logging;

namespace Coiny.API.Tests;
public sealed class OperationalJournalTests
{
    private static async Task Until(Func<bool> check) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!check()) await Task.Delay(10, timeout.Token);
    }
    [Fact]
    public async Task UnsafeLoggerStateAndExceptionMessagesNeverReachJournalAndPersistenceSurvivesRestart() {
        var path = Path.Combine(Path.GetTempPath(), "coiny-log-test-" + Guid.NewGuid().ToString("N"));
        var options = new JournalOptions { Directory = path };
        try {
            using (var journal = new OperationalJournal(options)) {
                await journal.StartAsync(default);
                var logger = new SafeJournalProvider(journal).CreateLogger("Coiny.API.Test");
                var secret = "synthetic-secret-financial-payload";
                logger.LogError(new InvalidOperationException(secret), "Password {Password} SQL {Sql}", secret, secret);
                journal.Record("api", "Warning", "request.failed", "test-trace", ("password", secret));
                await Until(() => journal.Snapshot().Length >= 3);
                await journal.StopAsync(default);
                var all = string.Join("\n", Directory.GetFiles(path).Select(File.ReadAllText));
                Assert.DoesNotContain(secret, all); Assert.Contains("InvalidOperationException", all);
                Assert.Contains("HTTP request failed", all);
                Assert.DoesNotContain("Password", all);
                if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Directory.GetFiles(path)[0]));
            }
            using var reopened = new OperationalJournal(options); await reopened.StartAsync(default);
            await Until(() => reopened.Snapshot().Any(e => e.RequestId == "test-trace"));
            Assert.Contains(reopened.Snapshot(), e => e.RequestId == "test-trace");
            await reopened.StopAsync(default);
        } finally { Directory.Delete(path, true); }
    }
    [Fact]
    public async Task RetentionPrunesOldAndExcessFilesAndKeepsMemoryBounded() {
        var path = Path.Combine(Path.GetTempPath(), "coiny-log-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
        try {
            for (int i = 0; i < 6; i++) {
                var file = Path.Combine(path, $"coiny-2020010{i}.jsonl"); File.WriteAllText(file, ""); File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-10));
            }
            using var journal = new OperationalJournal(new JournalOptions { Directory = path, MaxFiles = 2, FileSizeMb = 1 });
            await journal.StartAsync(default); await Until(() => journal.Snapshot().Length > 0);
            Assert.True(Directory.GetFiles(path, "coiny-*.jsonl").Length <= 2);
            for (int batch = 0; batch < 7; batch++) {
                for (int i = 0; i < 1000; i++) journal.Record("api", "Information", "request.completed", $"batch-{batch}-{i}", ("route", new string('r', 190)));
                var last = $"batch-{batch}-999";
                await Until(() => journal.Snapshot().Any(e => e.RequestId == last));
            }
            await journal.StopAsync(default);
            Assert.Equal(OperationalJournal.RecentLimit, journal.Snapshot().Length);
            Assert.Equal(2, Directory.GetFiles(path, "coiny-*.jsonl").Length);
            Assert.All(Directory.GetFiles(path, "coiny-*.jsonl"), f => Assert.True(new FileInfo(f).Length <= 1024 * 1024));
        } finally { Directory.Delete(path, true); }
    }
    [Fact]
    public async Task ImmediateShutdownDrainsEventsAcceptedBeforeWorkerStarts() {
        for (var attempt = 0; attempt < 20; attempt++) {
            using var journal = new OperationalJournal(new JournalOptions());
            journal.Record("api", "Information", "request.completed", "accepted-before-start");
            await journal.StartAsync(default); await journal.StopAsync(default);
            Assert.Contains(journal.Snapshot(), e => e.RequestId == "accepted-before-start");
        }
    }
    [Fact]
    public async Task FullQueueDoesNotBlockAndReportsLoss() {
        using var journal = new OperationalJournal(new JournalOptions());
        for (int i = 0; i < 3000; i++) journal.Record("api", "Information", "request.completed");
        Assert.True(journal.Dropped > 0); await journal.StartAsync(default); await journal.StopAsync(default);
        Assert.NotEmpty(journal.Snapshot());
    }
    [Fact]
    public async Task StorageFailureKeepsCollectingInMemory() {
        var path = Path.GetTempFileName();
        try {
            using var journal = new OperationalJournal(new JournalOptions { Directory = path });
            await journal.StartAsync(default); journal.Record("api", "Error", "request.failed");
            await Until(() => journal.Snapshot().Any(e => e.Message == "HTTP request failed")); await journal.StopAsync(default);
            Assert.True(journal.WriteFailures > 0);
        } finally { File.Delete(path); }
    }
}
