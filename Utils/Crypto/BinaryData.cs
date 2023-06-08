using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace SecureArchive.Utils.Crypto {
    internal class BinaryData {
        private IBuffer source = null!;
        public BinaryData(IBuffer source) {
            this.source = source;
        }
        public BinaryData(byte[] source) {
            this.source = source.AsBuffer();
        }

        private BinaryData() { }

        public IBuffer Buffer => source;
        public static BinaryData Create(byte[] source) => new BinaryData(source);
        public static BinaryData Create(IBuffer source) => new BinaryData(source);
        public static BinaryData Create(string source) => new BinaryData(source.AsBuffer());
        public static BinaryData FromHexString(string hex) => new BinaryData() { HexString = hex };
        public static BinaryData FromBase64String(string base64) => new BinaryData() { Base64String = base64 };

        public string HexString {
            get => CryptographicBuffer.EncodeToHexString(source);
            set { source = CryptographicBuffer.DecodeFromHexString(value); }
        }
        public string Base64String {
            get => CryptographicBuffer.EncodeToBase64String(source);
            set { source = CryptographicBuffer.DecodeFromBase64String(value); }
        }
    }
}
