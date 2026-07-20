using System.Data.Common;
using System.Threading.Tasks;
using MySqlConnector;

namespace Ashenfall.Data;

public sealed class Db
{
    private readonly string _connectionString;

    public Db(string connectionString) => _connectionString = connectionString;

    public async Task<DbConnection> OpenAsync()
    {
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }
}
