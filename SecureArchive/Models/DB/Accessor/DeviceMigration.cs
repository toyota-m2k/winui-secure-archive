using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB.Accessor;

public interface IDeviceMigration {
    DeviceMigrationInfo? Get(string oldOwnerId, int slot, string oldOriginalId);
    IList<DeviceMigrationInfo> List();
}

public interface IMutableDeviceMigration:IDeviceMigration {
    DeviceMigrationInfo? Add(string oldOwnerId, int slot, string oldOriginalId, string newOwnerId, string newOrignalId, DateTime? migratedOn = null);
    void Remove(DeviceMigrationInfo del);
}

public class DeviceMigration : IMutableDeviceMigration {
    private DBConnector _connector;
    private DbSet<DeviceMigrationInfo> _migrationInfos;
    public DeviceMigration(DBConnector connector) {
        _connector = connector;
        _migrationInfos = connector.DeviceMigrationInfos;
    }
    public DeviceMigrationInfo? Add(string oldOwnerId, int slot, string oldOriginalId, string newOwnerId, string newOrignalId, DateTime? migratedOn = null) {
        lock (_connector) {
            var rec = Get(oldOwnerId, slot, oldOriginalId);
            if(rec != null) {
                return null;    // already registered
            }
            rec = new DeviceMigrationInfo() {
                OldOwnerId = oldOwnerId,
                OldOriginalId = oldOriginalId,
                Slot = slot,
                NewOwnerId = newOwnerId,
                NewOriginalId = newOrignalId,
                MigratedOn = migratedOn?.Ticks ?? DateTime.Now.Ticks
            };
            return _migrationInfos.Add(rec)?.Entity;
        }
    }

    public DeviceMigrationInfo? Get(string oldOwnerId, int slot, string oldOriginalId) {
        lock(_connector) {
            return _migrationInfos.FirstOrDefault(x => x.OldOwnerId == oldOwnerId && x.Slot == slot && x.OldOriginalId == oldOriginalId);
        }
    }

    public IList<DeviceMigrationInfo> List() {
        lock(_connector) {
            return _migrationInfos.OrderBy(it=>it.Key).ToList();
        }
    }

    public void Remove(DeviceMigrationInfo del) {
        lock (_connector) {
            _migrationInfos.Remove(del);
        }
    }
}
