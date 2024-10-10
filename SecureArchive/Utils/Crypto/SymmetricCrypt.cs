using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
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
#if false
            // もともと、これで正しく動いていたが、ある日突然、decrypted.ToArray()が例外
            //  ArgumentException(SR.Argument_DestinationTooShort, "destination")
            // を投げるようになってしまった。何が原因か全く不明。
            // 内部で、IBuffer.CopyTo()
            return Encoding.UTF8.GetString(decrypted.ToArray());

            // IBuffer.ToArray() は、内部で IBuffer.CopyTo()を呼んでいるが、
            // 
            // var buff = new byte[decrypted.Length];
            // decrypted.CopyTo(0u, buff, 0, (int)decrypted.Length);
            //
            // としても、同じ例外が出る。ちゃんと destinationのバッファを確保しているのに。。。
            // 例外が出たところでブレイクすると、ソースの長さ（_length)と、ディスティネーションの長さ（destination.Length）を比較している箇所、
            // if ((uint)_length <= (uint)destination.Length)
            // で、
            //  destination.Length = 36 （decrypted.Lengthと同じ）
            //  _length = 48 （なんじゃこの数字は？）
            // となっていた。OSの更新かなにかで、WinRTにバグが混入したんじゃないか？
#else

            // 突然ダメになったから、そのうち、また突然イケるようになるかもしれない。
            try {
                decrypted.ToArray();
                UtLog.Instance("SymmetricCrypt").Info("decrypted.ToArray() is OK.");
            } catch (Exception) {

            }

            // 仕方がないから、CryptographicBuffer.CopyToByteArray() で代用する。
            // HashBuilder#Hash でも、IBuffer.ToArray() を使っているが、こちらはなぜか、正常に動作している。
            // CryptographicEngineが返してくる IBufferが異常なのかもしれない。
            // 昨日（2024/10/8）までは、正しく動いていたのに。。。
            var buff = new byte[decrypted.Length];
            CryptographicBuffer.CopyToByteArray(decrypted, out buff);
            return Encoding.UTF8.GetString(buff);
#endif
        }
    }
}
