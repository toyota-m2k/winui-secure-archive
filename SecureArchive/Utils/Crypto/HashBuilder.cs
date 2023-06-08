using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SecureArchive.Utils;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace SecureArchive.Utils.Crypto
{
    internal interface IHashResult {
        IBuffer HashBuffer { get; }
        byte[] Hash { get; }
        string AsHexString { get; }
        string AsBase64String { get; }

    }
    internal class HashBuilder : IHashResult {
        public static HashBuilder Create(string algorithm) { return new HashBuilder(algorithm); }
        public static HashBuilder MD5 => Create(HashAlgorithmNames.Md5);
        public static HashBuilder SHA1 => Create(HashAlgorithmNames.Sha1);
        public static HashBuilder SHA256 => Create(HashAlgorithmNames.Sha256);
        public static HashBuilder SHA384 => Create(HashAlgorithmNames.Sha384);
        public static HashBuilder SHA512 => Create(HashAlgorithmNames.Sha512);

        private HashAlgorithmProvider provider;
        private CryptographicHash maker;
        private IBuffer result = null!;

        private HashBuilder(string algorithm)
        {
            provider = HashAlgorithmProvider.OpenAlgorithm(algorithm);
            if (provider == null)
            {
                throw new InvalidOperationException("cannot open Hash Algorithm");
            }
            maker = provider.CreateHash();
            if (maker == null)
            {
                throw new InvalidOperationException("cannot create Hash Maker");
            }
        }

        public HashBuilder Append(IBuffer src)
        {
            maker.Append(src);
            return this;
        }
        public HashBuilder Append(string src)
        {
            maker.Append(Encoding.UTF8.GetBytes(src).AsBuffer());
            return this;
        }
        public HashBuilder Append(byte[] src)
        {
            maker.Append(src.AsBuffer());
            return this;
        }

        public HashBuilder Build()
        {
            result = maker.GetValueAndReset();
            return this;
        }

        public IBuffer HashBuffer => result;
        public byte[] Hash => result.ToArray();
        public string AsHexString => CryptographicBuffer.EncodeToHexString(result);
        public string AsBase64String => CryptographicBuffer.EncodeToBase64String(result);
    }
}
