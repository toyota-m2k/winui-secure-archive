using Windows.Storage;

namespace SecureArchive.Utils;
internal static class FileUtils {
    public static async Task MoveItemsInFolder(string src, string dst) {
        await Task.Run(async () => {
            if(!Path.Exists(dst)) {
                Directory.CreateDirectory(dst);
            }
            foreach(var file in Directory.GetFiles(src)) {
                var name = Path.GetFileName(file);
                var dstPath = Path.Combine(dst, name);
                File.Move(file, dst);
            }
            foreach(var dir in Directory.GetDirectories(src)) {
                var name = Path.GetFileName(dir);
                var dstPath = Path.Combine(dst, name);
                await MoveItemsInFolder(dir, dstPath);
                Directory.Delete(dir, true);
            }
        });
    }
    public static async Task CopyItemsInFolder(string src, string dst) {
        await Task.Run(async () => {
            if (!Path.Exists(dst)) {
                Directory.CreateDirectory(dst);
            }
            foreach (var file in Directory.GetFiles(src)) {
                var name = Path.GetFileName(file);
                var dstPath = Path.Combine(dst, name);
                File.Copy(file, dst);
            }
            foreach (var dir in Directory.GetDirectories(src)) {
                var name = Path.GetFileName(dir);
                var dstPath = Path.Combine(dst, name);
                await CopyItemsInFolder(dir, dstPath);
            }
        });
    }

    public static bool IsFolderEmpty(string path) {
        if(!Path.Exists(path)) return true;
        return Directory.EnumerateFileSystemEntries(path).Any();
    }


    public static async Task MoveItemsInFolder(StorageFolder src,  StorageFolder dst) { 
        foreach(var file in await src.GetFilesAsync()) {
            await file.MoveAsync(dst);
        }
        foreach (var dir in await src.GetFoldersAsync()) {
            var sub = await dst.CreateFolderAsync(dir.Name);
            await MoveItemsInFolder(dir, sub);
            await dir.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
    }

    public static async Task CopyItemsInFolder(StorageFolder src, StorageFolder dst) {
        foreach (var file in await src.GetFilesAsync()) {
            await file.CopyAsync(dst);
        }
        foreach (var dir in await src.GetFoldersAsync()) {
            var sub = await dst.CreateFolderAsync(dir.Name);
            await CopyItemsInFolder(dir, sub);
        }
    }

    public static async Task<bool> IsEmpty(this StorageFolder folder) {
        return (await folder.GetItemsAsync()).Count == 0;
    }

    internal static void SafeDelete(string outFilePath) {
        try {
            File.Delete(outFilePath);
        } catch {
            // nothing to do
        }
    }
}
