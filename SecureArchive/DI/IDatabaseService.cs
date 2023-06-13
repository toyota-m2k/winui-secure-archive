using SecureArchive.Models.DB.Accessor;

namespace SecureArchive.DI;

public interface ITables {
    IFileEntryList Entries { get; }
    IOwnerInfoList OwnerList { get; }
    IKVList KVs { get; }
}

public interface IMutableTables {
    IMutableFileEntryList Entries { get; }
    IMutableOwnerInfoList OwnerList { get; }
    IMutableKVList KVs { get; }
}

public interface IDatabaseService : ITables {
    bool Transaction(Func<IMutableTables, bool> fn);
    bool EditEntry(Func<IMutableFileEntryList, bool> fn);
    bool EditKVs(Func<IMutableKVList, bool> fn);
    bool EditOwnerList(Func<IMutableOwnerInfoList, bool> fn);
    void Update();

}
