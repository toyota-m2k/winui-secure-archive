using System.Security.Cryptography;
using System.Text.RegularExpressions;
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
                File.Copy(file, dstPath);
            }
            foreach (var dir in Directory.GetDirectories(src)) {
                var name = Path.GetFileName(dir);
                var dstPath = Path.Combine(dst, name);
                await CopyItemsInFolder(dir, dstPath);
            }
        });
    }

    public static async Task DeleteFolder(string path) {
        if (!Directory.Exists(path)) {
            return; // 最初から存在しない
        }

        async Task delete(string path) {
            foreach (var file in Directory.GetFiles(path)) {
                var name = Path.GetFileName(file);
                var dstPath = Path.Combine(path, name);
                File.Delete(dstPath);
            }
            foreach (var dir in Directory.GetDirectories(path)) {
                var name = Path.GetFileName(dir);
                var dstPath = Path.Combine(path, name);
                await delete(dstPath);
            }
        }

        await Task.Run(async () => {
            await delete(path);
        });
    }

    public static bool IsFolderEmpty(string path) {
        return !Directory.EnumerateFileSystemEntries(path).Any();
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

    internal static bool SafeDelete(string outFilePath) {
        try {
            File.Delete(outFilePath);
            return true;
        } catch {
            return false;
        }
    }
    internal static string SafeNameOf(string name) {
        // ファイル名に使えない文字を取得
        string invalidChars = new string(Path.GetInvalidFileNameChars());

        // ファイル名に使えない文字と、それに対応する全角文字の辞書を作成
        Dictionary<char, char> replaceChars = new Dictionary<char, char>()
        {
            {'\\', '￥'},
            {'/', '／'},
            {':', '：'},
            {'*', '＊'},
            {'?', '？'},
            {'"', '”'},
            {'<', '＜'},
            {'>', '＞'},
            {'|', '｜'}
        };

        // 正規表現で置き換える
        Regex regex = new Regex($"[{Regex.Escape(invalidChars)}]");
        return regex.Replace(name, m => replaceChars[m.Value[0]].ToString());
    }
}
