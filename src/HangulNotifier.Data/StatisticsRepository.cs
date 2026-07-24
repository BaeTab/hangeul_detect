using Microsoft.Data.Sqlite;

namespace HangulNotifier.Data;

public sealed record RuleCount(string RuleId, int Count);
public sealed record DailyCount(string Day, int Count);

public interface IStatisticsRepository
{
    void Record(string ruleId, string? process, DateTimeOffset when);
    int CountSince(DateTimeOffset since);
    IReadOnlyList<RuleCount> TopRules(int limit, DateTimeOffset since);
    IReadOnlyList<DailyCount> DailySeries(int days, DateTimeOffset now);
    void ClearAll();
}

/// <summary>
/// SQLite 통계 저장소. 규칙ID·감지시각·프로세스명만 저장한다.
/// 입력 텍스트/창 제목/파일 경로는 절대 저장하지 않는다.
/// </summary>
public sealed class StatisticsRepository : IStatisticsRepository, IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _gate = new();

    public StatisticsRepository(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        _conn.Open();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS detections (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                rule_id     TEXT NOT NULL,
                detected_at INTEGER NOT NULL,
                process     TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_detected_at ON detections(detected_at);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Record(string ruleId, string? process, DateTimeOffset when)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO detections(rule_id, detected_at, process) VALUES ($r, $t, $p)";
            cmd.Parameters.AddWithValue("$r", ruleId);
            cmd.Parameters.AddWithValue("$t", when.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$p", (object?)process ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public int CountSince(DateTimeOffset since)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM detections WHERE detected_at >= $s";
            cmd.Parameters.AddWithValue("$s", since.ToUnixTimeSeconds());
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public IReadOnlyList<RuleCount> TopRules(int limit, DateTimeOffset since)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT rule_id, COUNT(*) c FROM detections
                WHERE detected_at >= $s
                GROUP BY rule_id ORDER BY c DESC LIMIT $n
                """;
            cmd.Parameters.AddWithValue("$s", since.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$n", limit);
            var list = new List<RuleCount>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(new RuleCount(r.GetString(0), r.GetInt32(1)));
            return list;
        }
    }

    public IReadOnlyList<DailyCount> DailySeries(int days, DateTimeOffset now)
    {
        var since = now.AddDays(-(days - 1)).Date;
        var sinceOffset = new DateTimeOffset(since, now.Offset);
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT date(detected_at, 'unixepoch', 'localtime') d, COUNT(*) c
                FROM detections WHERE detected_at >= $s
                GROUP BY d ORDER BY d
                """;
            cmd.Parameters.AddWithValue("$s", sinceOffset.ToUnixTimeSeconds());
            var map = new Dictionary<string, int>();
            using (var r = cmd.ExecuteReader())
                while (r.Read()) map[r.GetString(0)] = r.GetInt32(1);

            // 빈 날짜도 0으로 채워 연속 시계열 생성
            var result = new List<DailyCount>(days);
            for (int i = 0; i < days; i++)
            {
                var day = since.AddDays(i);
                var key = day.ToString("yyyy-MM-dd");
                result.Add(new DailyCount(key, map.TryGetValue(key, out var c) ? c : 0));
            }
            return result;
        }
    }

    public void ClearAll()
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM detections; VACUUM;";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose() => _conn.Dispose();
}
