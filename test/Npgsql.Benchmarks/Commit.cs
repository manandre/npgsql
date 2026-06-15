using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

// ReSharper disable AssignNullToNotNullAttribute.Global

namespace Npgsql.Benchmarks;

[OperationsPerSecond]
public class Commit
{
    readonly NpgsqlConnection _conn;
    readonly NpgsqlCommand _cmd;

    public Commit()
    {
        _conn = BenchmarkEnvironment.OpenConnection();
        _cmd = new NpgsqlCommand("SELECT 1", _conn);
    }

    [GlobalCleanup]
    public void Cleanup() => _conn.Dispose();

    [Benchmark]
    public void Basic()
    {
        var tx = _conn.BeginTransaction();
        _cmd.ExecuteNonQuery();
        tx.Commit();
    }

    [Benchmark]
    public async Task BasicAsync()
    {
        await using var tx = await _conn.BeginTransactionAsync();
        await _cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
}
