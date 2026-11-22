using System.Diagnostics;
using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Diagnostics;

/// <summary>
/// The data-layer timer: each repository operation is timed, traced (one span) and logged (one entry),
/// without parameter values or exception messages, which can contain personal data.
/// </summary>
public sealed class DataAccessInstrumentationTests : IDisposable
{
    private const string PersonalData = "someone@example.com";

    private readonly FakeLogger<DataAccessInstrumentation> _logger = new();
    private readonly FakeTimeProvider _time = new();

    // A unique table name isolates this test's spans from tests running in parallel.
    private readonly string _table = $"Heroes_{Guid.NewGuid():N}";
    private readonly List<Activity> _spans = [];
    private readonly ActivityListener _listener;

    public DataAccessInstrumentationTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DataAccessInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.DisplayName.EndsWith(_table, StringComparison.Ordinal))
                {
                    lock (_spans)
                    {
                        _spans.Add(activity);
                    }
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    // Logging

    [Fact]
    public void Insert_logs_the_table_new_id_and_elapsed_time_at_Information()
    {
        using (var operation = Create().Begin(DataOperation.Insert, _table))
        {
            _time.Advance(TimeSpan.FromMilliseconds(15));
            operation.Succeeded(rowCount: 1, entityId: 42);
        }

        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        record.Message.ShouldBe($"Inserted {_table} row 42 in 15 ms");
        State(record).ShouldContainKeyAndValue("Table", _table);
        State(record).ShouldContainKeyAndValue("EntityId", "42");
        State(record).ShouldContainKeyAndValue("ElapsedMs", "15");
    }

    [Fact]
    public void Update_logs_the_rows_affected_at_Information()
    {
        using (var operation = Create().Begin(DataOperation.Update, _table, entityId: 7))
        {
            _time.Advance(TimeSpan.FromMilliseconds(3));
            operation.Succeeded(rowCount: 1);
        }

        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        record.Message.ShouldBe($"Updated {_table} row 7 (1 row(s) affected) in 3 ms");
    }

    [Fact]
    public void Soft_delete_logs_at_Information()
    {
        using (var operation = Create().Begin(DataOperation.SoftDelete, _table, entityId: 7))
        {
            _time.Advance(TimeSpan.FromMilliseconds(4));
            operation.Succeeded(rowCount: 1);
        }

        _logger.Collector.LatestRecord.Message.ShouldBe($"Soft-deleted {_table} row 7 (1 row(s) affected) in 4 ms");
        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public void Select_logs_the_row_count_at_Debug_because_reads_are_frequent()
    {
        using (var operation = Create().Begin(DataOperation.Select, _table))
        {
            _time.Advance(TimeSpan.FromMilliseconds(2));
            operation.Succeeded(rowCount: 3);
        }

        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Debug);
        record.Message.ShouldBe($"Selected 3 {_table} rows in 2 ms");
    }

    [Fact]
    public void Operations_slower_than_the_threshold_also_log_a_Warning()
    {
        using (var operation = Create(slowThresholdMs: 100).Begin(DataOperation.Insert, _table))
        {
            _time.Advance(TimeSpan.FromMilliseconds(250));
            operation.Succeeded(rowCount: 1, entityId: 1);
        }

        var warning = _logger.Collector.GetSnapshot().Where(r => r.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldBe($"Slow INSERT on {_table}: 250 ms (threshold 100 ms)");
    }

    [Theory]
    [InlineData(100, 100)] // exactly at the threshold is not slow
    [InlineData(0, 5_000)] // a threshold of 0 disables the warning
    public void No_slow_warning_at_or_below_the_threshold_or_when_disabled(int thresholdMs, int elapsedMs)
    {
        using (var operation = Create(thresholdMs).Begin(DataOperation.Select, _table))
        {
            _time.Advance(TimeSpan.FromMilliseconds(elapsedMs));
            operation.Succeeded(rowCount: 0);
        }

        _logger.Collector.GetSnapshot().ShouldNotContain(r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public void Failures_log_an_Error_with_the_exception_type_but_never_its_message()
    {
        // SQL Server echoes data in some messages, for example duplicate key violations.
        var exception = new InvalidOperationException($"The duplicate key value is ({PersonalData}).");

        using (var operation = Create().Begin(DataOperation.Insert, _table))
        {
            _time.Advance(TimeSpan.FromMilliseconds(8));
            operation.Failed(exception);
        }

        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldBe(
            $"INSERT on {_table} failed after 8 ms: System.InvalidOperationException (SQL error (null))");
        record.Exception.ShouldBeNull();
        record.Message.ShouldNotContain(PersonalData);
        State(record).Values.ShouldAllBe(value => value == null || !value.Contains(PersonalData));
    }

    // Tracing

    [Fact]
    public void Each_operation_produces_one_span_named_after_the_operation_and_table()
    {
        using (var operation = Create().Begin(DataOperation.Insert, _table))
        {
            operation.Succeeded(rowCount: 1, entityId: 42);
        }

        var span = _spans.ShouldHaveSingleItem();
        span.DisplayName.ShouldBe($"INSERT {_table}");
        span.Kind.ShouldBe(ActivityKind.Internal);
        span.Status.ShouldBe(ActivityStatusCode.Unset);
        span.GetTagItem("db.system.name").ShouldBe("microsoft.sql_server");
        span.GetTagItem("db.operation.name").ShouldBe("INSERT");
        span.GetTagItem("db.collection.name").ShouldBe(_table);
        span.GetTagItem("heroregistry.entity.id").ShouldBe("42");
        span.GetTagItem("heroregistry.rows_affected").ShouldBe(1);
    }

    [Fact]
    public void Select_spans_record_the_returned_row_count()
    {
        using (var operation = Create().Begin(DataOperation.Select, _table))
        {
            operation.Succeeded(rowCount: 3);
        }

        _spans.ShouldHaveSingleItem().GetTagItem("db.response.returned_rows").ShouldBe(3);
    }

    [Fact]
    public void Soft_delete_spans_are_SQL_updates()
    {
        using (var operation = Create().Begin(DataOperation.SoftDelete, _table, entityId: 7))
        {
            operation.Succeeded(rowCount: 1);
        }

        var span = _spans.ShouldHaveSingleItem();
        span.DisplayName.ShouldBe($"SOFT DELETE {_table}");
        span.GetTagItem("db.operation.name").ShouldBe("UPDATE");
    }

    [Fact]
    public void Failed_spans_are_marked_as_errors_without_the_exception_message()
    {
        using (var operation = Create().Begin(DataOperation.Update, _table, entityId: 1))
        {
            operation.Failed(new InvalidOperationException($"value ({PersonalData})"));
        }

        var span = _spans.ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.StatusDescription.ShouldBe(nameof(InvalidOperationException));
        span.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        span.Events.ShouldBeEmpty();
        span.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(PersonalData));
    }

    [Fact]
    public void Operations_disposed_without_an_outcome_are_marked_as_errors_and_not_logged()
    {
        using (Create().Begin(DataOperation.Select, _table))
        {
            // An exception escaped before Succeeded or Failed was called.
        }

        _spans.ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Error);
        _logger.Collector.Count.ShouldBe(0);
    }

    // Guard clauses

    [Fact]
    public void An_operation_can_only_complete_once()
    {
        using var operation = Create().Begin(DataOperation.Select, _table);
        operation.Succeeded(rowCount: 1);

        Should.Throw<InvalidOperationException>(() => operation.Succeeded(rowCount: 1));
        Should.Throw<InvalidOperationException>(() => operation.Failed(new InvalidOperationException()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Begin_requires_a_table_name(string table)
    {
        Should.Throw<ArgumentException>(() => Create().Begin(DataOperation.Select, table));
    }

    [Fact]
    public void Row_count_cannot_be_negative()
    {
        using var operation = Create().Begin(DataOperation.Select, _table);

        Should.Throw<ArgumentOutOfRangeException>(() => operation.Succeeded(rowCount: -1));
    }

    private DataAccessInstrumentation Create(int slowThresholdMs = 500) =>
        new(_logger,
            Options.Create(new DatabaseOptions { SlowCommandThresholdMs = slowThresholdMs }),
            _time);

    private static Dictionary<string, string?> State(FakeLogRecord record) =>
        record.StructuredState!.ToDictionary(kv => kv.Key, kv => kv.Value);
}
