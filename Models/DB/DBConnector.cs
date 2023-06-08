using Microsoft.EntityFrameworkCore;
using SecureArchive.Models.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Management.Deployment.Preview;

namespace SecureArchive.Models.DB;
public class DBConnector : DbContext
{
    public DbSet<Entry> Entries { get; set; }
    public DbSet<OwnerInfo> OwnerInfos { get; set; }
    public DbSet<KV> KVs { get; set; }

    private string _dbPath;
    public DBConnector(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    //protected override void OnModelCreating(ModelBuilder modelBuilder) {
    //    base.OnModelCreating(modelBuilder);
    //}

}
