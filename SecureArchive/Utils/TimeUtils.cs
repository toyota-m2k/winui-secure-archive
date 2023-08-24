namespace SecureArchive.Utils;
public static class TimeUtils {
    private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

    /**
     * DateTimeを Java の Date#time の値に変換する。
     */
    public static long dateTime2javaTime(DateTime time) {
        TimeSpan span = time - Epoch;
        return (long)Math.Round(span.TotalMilliseconds);
    }
}
