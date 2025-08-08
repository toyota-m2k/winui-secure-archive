using SecureArchive.Models.DB.Accessor;

namespace SecureArchive.DI;

public interface ITables {
    IFileEntryList Entries { get; }
    IOwnerInfoList OwnerList { get; }
    IKVList KVs { get; }
    IDeviceMigration DeviceMigration { get; }
}

public interface IMutableTables {
    IMutableFileEntryList Entries { get; }
    IMutableOwnerInfoList OwnerList { get; }
    IMutableKVList KVs { get; }
    IMutableDeviceMigration DeviceMigration { get; }
}

public interface IDatabaseService : ITables {
    bool Transaction(Func<IMutableTables, bool> fn);
    bool EditEntry(Func<IMutableFileEntryList, bool> fn);
    bool EditKVs(Func<IMutableKVList, bool> fn);
    bool EditOwnerList(Func<IMutableOwnerInfoList, bool> fn);
    bool EditDeviceMigration(Func<IMutableDeviceMigration, bool> fn);
    void Update();
    void Dispose();
}
