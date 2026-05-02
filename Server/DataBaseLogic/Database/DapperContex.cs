using System;
using System.Data;
using Npgsql;

namespace Server.Database;

class DapperContext {
    private readonly string connectionString = "Server=localhost; Port=5432; Username=postgres; Password=1234; Database=tcp";
    public IDbConnection DbConnection => new NpgsqlConnection(connectionString);
}