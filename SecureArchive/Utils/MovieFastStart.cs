using SecureArchive.DI;
using System.Diagnostics;
using System.Text;

namespace SecureArchive.Utils;

internal class MovieFastStart {
    static UtLog logger = new UtLog(typeof(MovieFastStart));
    private const int CHUNK_SIZE = 8192;

    public Exception? LastException { get; private set; } = null;
    public long OutputLength { get; private set; } = 0L;
    public string TaskName { get; set; } = ""; 

    class Atom {
        public string type;
        public ulong size;
        public ulong start = 0;
        public Atom(string type, ulong size) {
            this.type = type;
            this.size = size;
        }
        public override string ToString() {
            return $"[{type}, start={start}, size={size}]";
        }
    }

    private static async Task<bool> read(Stream inputStream, byte[] buffer) {
        if (await inputStream.ReadAsync(buffer) != buffer.Length) {
            return false;
        }
        return true;
    }

    private static uint toUInt(byte[] buffer) {
        uint n = 0;
        foreach (var b in buffer) {
            n = (n << 8) + ((uint)b & 0xFF);
        }
        return n;
    }
    private static ulong toULong(byte[] buffer) {
        ulong n = 0;
        foreach (var b in buffer) {
            n = (n << 8) + ((uint)b & 0xFF);
        }
        return n;
    }

    private static async Task<uint?> readInt(Stream inputStream) {
        var buffer = new byte[4];
        if (!await read(inputStream, buffer)) return null;
        return toUInt(buffer);
    }
    private static async Task<uint> readIntOrThrow(Stream inputStream) {
        var n = await readInt(inputStream);
        if (!n.HasValue) {
            throw new Exception("invalid length");
        }
        return n.Value;
    }

    private static async Task<ulong?> readLong(Stream inputStream) {
        var buffer = new byte[8];
        if (!await read(inputStream, buffer)) return null;
        return toULong(buffer);
    }

    private static async Task<ulong> readLongOrThrow(Stream inputStream) {
        var n = await readLong(inputStream);
        if (!n.HasValue) {
            throw new Exception("invalid length");
        }
        return n.Value;
    }

    private static ASCIIEncoding encoding = new ASCIIEncoding();
    private static async Task<string?> readType(Stream inputStream) {
        var buffer = new byte[4];
        if (!await read(inputStream, buffer)) return null;
        return encoding.GetString(buffer);
    }

    private static async Task<Atom?> readAtom(Stream inputStream) {
        try {
            var size = await readInt(inputStream);
            if (size == null) return null;
            var type = await readType(inputStream);
            if(type == null) return null;

            return new Atom(type, (uint)size);
        } catch(Exception) {
            return null;
        }
    }

    private static byte[] toByteArray(uint n) {
        var b = new byte[4];
        for (var i = 0; i < 4; i++) {
            b[3 - i] = (byte)(n >> (8 * i));
        }
        Debug.Assert(n == toUInt(b));
        return b;
    }
    private static byte[] toByteArray(ulong n) {
        var b = new byte[8];
        for (var i = 0; i < 8; i++) {
            b[7 - i] = (byte)(n >> (8 * i));
        }
        Debug.Assert(n == toULong(b));
        return b;
    }

    private static async Task<List<Atom>?> getIndex(Stream inputStream) {
        logger.Debug("Getting index of top level atoms...");
        var index = new List<Atom>();
        var seenMoov = false;
        var seenMdat = false;
        try {
            while (true) {
                var atom = await readAtom(inputStream);
                if (atom == null) break;
                ulong skippedBytes = 8;
                if (atom.size == 1L) {
                    atom.size = await readLongOrThrow(inputStream);
                    skippedBytes = 16;
                }
                atom.start = (ulong)inputStream.Position - skippedBytes;
                index.Add(atom);
                logger.Debug(atom.ToString());
                if (atom.type.Equals("moov", StringComparison.OrdinalIgnoreCase)) seenMoov = true;
                if (atom.type.Equals("mdat")) seenMdat = true;
                if (atom.size == 0L) break;
                // inputStream.Seek((long)atom.size - (long)skippedBytes, SeekOrigin.Current);
                inputStream.Position += ((long)atom.size - (long)skippedBytes);
            }
        }
        catch (Exception e) {
            logger.Error(e);
        }
        if (!seenMoov) {
            logger.Error("No moov atom found.");
            return null;
        }
        if (!seenMdat) {
            logger.Error("No mdat atom found.");
            return null;
        }
        return index;
    }

    private static async Task<Atom?> skipToNextTable(Stream inputStream) {
        while (true) {
            var atom = await readAtom(inputStream);
            if (atom == null) break;
            if (isTableType(atom.type)) {
                return atom;
            } else if (isKnownAncestorType(atom.type)) {
                continue;
            } else {
                // inputStream.Seek((long)atom.size - 8, SeekOrigin.Current);
                inputStream.Position += ((long)atom.size - 8);
            }
        }
        return null;
    }

    private static bool isKnownAncestorType(string type) {
        return
            type.Equals("trak", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("mdia", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("minf", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("stbl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool isTableType(string type) {
        return
        type.Equals("stco", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("co64", StringComparison.OrdinalIgnoreCase);
    }

    public delegate Stream OutputStreamFactory();

    private async Task<bool> processImpl(Stream inputStream, OutputStreamFactory? outputStreamFactory, bool removeFree, UpdateMessageProc? updateMessageProc, ProgressProc? progressProc) {
        OutputLength = 0L;
        progressProc?.Invoke(0, 0);
        updateMessageProc?.Invoke($"{TaskName}- Getting index of top level atoms...");

        var index = await getIndex(inputStream);
        if(index == null) return false;

        ulong mdatStart = 999999L;
        ulong freeSize = 0L;

        Atom moov = null!;

        //Check that moov is after mdat (they are both known to exist)
        foreach (Atom atom in index) {
            if (atom.type.Equals("moov", StringComparison.OrdinalIgnoreCase)) {
                moov = atom;
            }
            else if (atom.type.Equals("mdat", StringComparison.OrdinalIgnoreCase)) {
                mdatStart = atom.start;
            }
            else if (atom.type.Equals("free", StringComparison.OrdinalIgnoreCase) && atom.start < mdatStart) {
                //This free atom is before the mdat
                freeSize += atom.size;
            }
        }

        int offset = (int)((long)moov.size - (long)freeSize);

        if (moov.start < mdatStart) {
            offset -= (int)moov.size;
            if (!removeFree || freeSize == 0) {
                //good to go already!
                updateMessageProc?.Invoke($"{TaskName}- File already suitable.");
                logger.Info("File already suitable.");
                return false;
            }
        }
        if (outputStreamFactory == null) {
            return true;
        }

        updateMessageProc?.Invoke($"{TaskName}- Patching moov...");
        byte[] moovContents = new byte[(int)moov.size];
        try {
            //inputStream.Seek((long)moov.start, SeekOrigin.Begin);
            inputStream.Position = (long)moov.start;
            await inputStream.ReadAsync(moovContents);
        }
        catch (IOException ex) {
            logger.Error(ex);
            return false;
        }

        using (var moovIn = new MemoryStream(moovContents))
        using (var moovOut = new MemoryStream(moovContents.Length)) {

            //Skip type and size
            //moovIn.Seek(8, SeekOrigin.Begin);
            moovIn.Position = 8;
            try {
                Atom? atom;
                while ((atom = await skipToNextTable(moovIn)) != null) {
                    moovIn.Position += 4; //skip version and flags
                    uint entryCount = await readIntOrThrow(moovIn);
                    logger.Info("Patching " + atom.type + " with " + entryCount + " entries.");

                    int entriesStart = (int)moovIn.Position;
                    //write up to start of the entries
                    moovOut.Write(moovContents, (int)moovOut.Length, entriesStart - (int)moovOut.Length);

                    if (atom.type.Equals("stco", StringComparison.OrdinalIgnoreCase)) { //32 bit
                        for (int i = 0; i < entryCount; i++) {
                            var entry = toByteArray((uint)((int)await readIntOrThrow(moovIn) + offset));
                            moovOut.Write(entry);
                        }
                    }
                    else { //64 bit
                        for (int i = 0; i < entryCount; i++) {
                            var entry = toByteArray((ulong)((long)await readLongOrThrow(moovIn) + offset));
                            moovOut.Write(entry);
                        }
                    }
                }

                if (moovOut.Length < moovContents.Length) { //write the rest
                    moovOut.Write(moovContents, (int)moovOut.Length, moovContents.Length - (int)moovOut.Length);
                }

            }
            catch (Exception ex) {
                logger.Error(ex);
                return false;
            }


            logger.Debug("Writing output file:");
            using (var outputStream = outputStreamFactory()) {
                long totalLength = 0L;

                //write ftype
                var ftyp = index.First(a => a.type.Equals("ftyp", StringComparison.OrdinalIgnoreCase));
                updateMessageProc?.Invoke($"{TaskName}- Writing ftyp...");
                logger.Debug("Writing ftyp...");
                try {
                    inputStream.Position = (long)ftyp.start;
                    var buffer = new byte[ftyp.size];
                    if (!await read(inputStream, buffer)) {
                        throw new Exception("invalid ftyp");
                    }
                    await outputStream.WriteAsync(buffer);
                    totalLength += (long)ftyp.size;
                }
                catch (Exception ex) {
                    logger.Error(ex);
                    return false;
                }

                logger.Debug("Writing moov...");
                try {
                    moovOut.Position = 0;
                    await moovOut.CopyToAsync(outputStream);
                    totalLength += moovOut.Length;
                }
                catch (Exception ex) {
                    logger.Error(ex);
                    return false;
                }

                //write everything else!
                foreach (var atom in index) {
                    if (atom.type.Equals("ftyp", StringComparison.OrdinalIgnoreCase) ||
                        atom.type.Equals("moov", StringComparison.OrdinalIgnoreCase) ||
                        atom.type.Equals("free", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    updateMessageProc?.Invoke($"{TaskName}- Writing {atom.type} ...");
                    logger.Debug("Writing " + atom.type + "...");

                    try {
                        inputStream.Position = (long)atom.start;
                        byte[] chunk = new byte[CHUNK_SIZE];
                        long atomLength = (long)atom.size;
                        while(atomLength>0) { 
                            var len = await inputStream.ReadAsync(chunk);
                            if(len == 0) {
                                throw new Exception("invalid atom: " + atom.type);
                            }
                            await outputStream.WriteAsync(chunk, 0, len);
                            atomLength -= len;
                            progressProc?.Invoke((long)atom.size - atomLength, (long)atom.size);
                        }
                        totalLength += (long)atom.size;
                    }
                    catch (Exception ex) {
                        logger.Error(ex);
                        return false;
                    }
                }
                OutputLength = totalLength;
                await outputStream.FlushAsync();
            }
        }
        logger.Debug("Write complete!");
        return true;
    }
    
    public async Task<bool> Process(Stream inputStream, OutputStreamFactory outputStreamFactory, IStatusNotificationService? notificationService) {
        LastException = null;
        try {
            if (notificationService!=null) {
                bool result = false;
                await notificationService.WithProgress($"{TaskName ?? "Processing"}...", async (updateMessage, progress) => {
                    result = await processImpl(inputStream, outputStreamFactory, true, updateMessage, progress);
                });
                return result;
            } else {
                return await processImpl(inputStream, outputStreamFactory, true, null, null);
            }
        }
        catch (Exception ex) {
            LastException = ex;
            logger.Error(ex);
            return false;
        }
    }

    public async Task<bool> Check(Stream inputStream, bool removeFree = true) {
        LastException = null;
        try {
            return await processImpl(inputStream, null, removeFree, null, null);
        }
        catch (Exception ex) {
            LastException = ex;
            logger.Error(ex);
            return false;
        }
    }
}
