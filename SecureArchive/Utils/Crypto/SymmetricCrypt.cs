using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace SecureArchive.Utils.Crypto {
    internal class SymmetricCrypt {
        SymmetricKeyAlgorithmProvider _provider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);
        IBuffer hashedPassword;
        IBuffer iv;
        CryptographicKey key;

        public SymmetricCrypt(string password, string cryptSeed, string ivSeed) {
            hashedPassword = HashHelper.SHA256(password, cryptSeed).HashBuffer;
            iv = HashHelper.SHA256(password, ivSeed).HashBuffer;
            key = _provider.CreateSymmetricKey(hashedPassword);
        }

        public static SymmetricCrypt Create(string password, string cryptSeed, string ivSeed) => new SymmetricCrypt(password, cryptSeed, ivSeed);  

        public BinaryData EncryptString(string source) {
            var crypted = CryptographicEngine.Encrypt(key, Encoding.UTF8.GetBytes(source).AsBuffer(), iv);
            return new BinaryData(crypted);
        }

        public string DecryptString(BinaryData source) {
            var decrypted = CryptographicEngine.Decrypt(key, source.Buffer, iv);
            return Encoding.UTF8.GetString(decrypted.ToArray());
        }
    }
}
