using Microsoft.EntityFrameworkCore;
using SecureArchive.Models.DB;
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
        ExecuteRawSql(DB.KV.DDL);
        ExecuteRawSql(DB.OwnerInfo.DDL);
        ExecuteRawSql(DB.FileEntry.DDL);
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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        //optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        optionsBuilder.UseSqlite(mConnection);
        optionsBuilder.LogTo(msg => System.Diagnostics.Debug.WriteLine(msg));
    }

    //protected override void OnModelCreating(ModelBuilder modelBuilder) {
    //    base.OnModelCreating(modelBuilder);
    //}
}
