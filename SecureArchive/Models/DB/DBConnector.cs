using Microsoft.EntityFrameworkCore;
using SecureArchive.Models.DB;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Management.Deployment.Preview;

namespace SecureArchive.Models.DB;
public class DBConnector : DbContext
{
    public DbSet<FileEntry> Entries { get; set; }
    public DbSet<OwnerInfo> OwnerInfos { get; set; }
    public DbSet<KV> KVs { get; set; }

    public int MajorVersion { get; } = 1;
    public int MinorVersion { get; } = 1;

    public UtLog _logger = new UtLog("DBConnector");

    public static long DB_VERSION = 1L;

    private string _dbPath;
    private SQLiteConnection mConnection;
    public DBConnector(string dbPath)
    {
        _dbPath = dbPath;
        //bool creation = false;
        //if (dbPath == ":memory:" || !Path.Exists(dbPath)) {
        //    creation = true;
        //}

        var builder = new SQLiteConnectionStringBuilder { DataSource = dbPath };
        mConnection = new SQLiteConnection(builder.ConnectionString);
        mConnection.Open();
        InitTables();
    }

    private void InitTables() {
        var version = QueryLongRawSql("PRAGMA user_version");

        ExecuteRawSql(DB.KV.DDL);
        ExecuteRawSql(DB.OwnerInfo.DDL);
        ExecuteRawSql(DB.FileEntry.DDL);

        if(version < DB_VERSION) {
            // バージョンアップ
            var sqls = DB.FileEntry.Migrate(version, DB_VERSION);
            if (sqls != null) {
                ExecuteRawSql(sqls);
            }

            ExecuteRawSql($"PRAGMA user_version = {DB_VERSION}");
        }
    }

    private void ExecuteRawSql(params string[] sqls) {
        SQLiteCommandBuilder builder = new SQLiteCommandBuilder();
        if (mConnection == null) return;
        using (var cmd = mConnection.CreateCommand()) {
            foreach (var sql in sqls) {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
    }
    private void QueryRawSql(string sql, Action<SQLiteDataReader> action) {
        SQLiteCommandBuilder builder = new SQLiteCommandBuilder();
        if (mConnection == null) return;
        using (var cmd = mConnection.CreateCommand()) {
            cmd.CommandText = sql;
            using (var reader = cmd.ExecuteReader()) {
                action(reader);
            }
        }
    }
    private long QueryLongRawSql(string sql, long defValue = 0L) {
        var result = defValue;
        QueryRawSql(sql, (reader) => {
            if (reader.Read()) {
                result = reader.GetInt64(0);
            }
        });
        return result;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        //optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        optionsBuilder.UseSqlite(mConnection);

        // SQL をデバッグ出力するなら、以下を有効にする
        //optionsBuilder.LogTo(msg => System.Diagnostics.Debug.WriteLine(msg));
    }

    //protected override void OnModelCreating(ModelBuilder modelBuilder) {
    //    base.OnModelCreating(modelBuilder);
    //}
}
