﻿using SecureArchive.Models.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;
public interface IEntryCreator : IDisposable {
    Stream OutputStream { get; }
    FileEntry Complete(string name, long size, string type, long originalDate, string? metaInfo);
}

internal interface ISecureStorageService {
    bool IsRegistered(string ownerId, string originalId);
    bool IsRegistered(string ownerId, string originalId, long lastModified);
    Task<FileEntry?> RegisterFile(string filePath, string ownerId, string? name, long originalDate, string originalId, string? metaInfo, ProgressProc? progress);
    Task<FileEntry?> Register(Stream inStream, string ownerId, string name, long size, string type, long originalDate, string originalId, string? metaInfo, ProgressProc? progress);
    Task<IEntryCreator?> CreateEntry(string ownerId, string originalId, bool overwrite=false);

    Stream OpenEntry(FileEntry entry);
    Task Export(FileEntry entry, string outPath);
    Task Export(FileEntry entry, string outPath, ProgressProc progress);
}
