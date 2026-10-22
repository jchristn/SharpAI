namespace SharpAI.Helpers
{
    using System;

    /// <summary>Named hashes computed from the same stream. Treat the returned arrays as read-only when sharing between threads.</summary>
    public sealed class HashResults
    {
        private readonly byte[] _MD5;
        private readonly byte[] _SHA1;
        private readonly byte[] _SHA256;

        /// <summary>Create a set of hash results.</summary>
        /// <param name="md5">Non-null MD5 hash bytes.</param>
        /// <param name="sha1">Non-null SHA1 hash bytes.</param>
        /// <param name="sha256">Non-null SHA256 hash bytes.</param>
        /// <exception cref="ArgumentNullException">A supplied hash is null.</exception>
        public HashResults(byte[] md5, byte[] sha1, byte[] sha256)
        {
            _MD5 = md5 ?? throw new ArgumentNullException(nameof(md5));
            _SHA1 = sha1 ?? throw new ArgumentNullException(nameof(sha1));
            _SHA256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
        }

        /// <summary>Gets non-null MD5 hash bytes.</summary>
        public byte[] MD5 { get { return _MD5; } }

        /// <summary>Gets non-null SHA1 hash bytes.</summary>
        public byte[] SHA1 { get { return _SHA1; } }

        /// <summary>Gets non-null SHA256 hash bytes.</summary>
        public byte[] SHA256 { get { return _SHA256; } }
    }
}
