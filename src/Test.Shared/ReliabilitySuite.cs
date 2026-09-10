namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using SharpAI.Models;
    using SharpAI.Security;

    using Touchstone.Core;

    /// <summary>
    /// Reliability and fault-injection suite for the deterministic security/query surface: numeric clamping,
    /// token crypto against malformed input, constant-time password verification, key generation entropy,
    /// and anonymous-path/secure-compare logic. These are the "behaves correctly under malformed input"
    /// guarantees that back the reliability bar. No database or model is required.
    /// </summary>
    public static class ReliabilitySuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the reliability suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("Reliability", "EnumerationQuery_Clamps", "Page number/size are clamped to safe bounds",
                    ct =>
                    {
                        EnumerationQuery q = new EnumerationQuery();
                        q.PageSize = 0;
                        TestAssert.Equal(1, q.PageSize);
                        q.PageSize = 100000;
                        TestAssert.Equal(1000, q.PageSize);
                        q.PageNumber = 0;
                        TestAssert.Equal(1, q.PageNumber);
                        q.PageNumber = -7;
                        TestAssert.Equal(1, q.PageNumber);
                        q.PageNumber = 3;
                        q.PageSize = 25;
                        TestAssert.Equal(50, q.Offset);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Reliability", "Token_RoundTrip_And_Garbage", "Session tokens round-trip and reject malformed input",
                    ct =>
                    {
                        SessionTokenService tokens = new SessionTokenService("reliability-key");
                        string token = tokens.Encrypt("ses_abc123");
                        TestAssert.Equal("ses_abc123", tokens.Decrypt(token));

                        TestAssert.True(tokens.Decrypt(null) == null, "null token decrypts to null");
                        TestAssert.True(tokens.Decrypt("") == null, "empty token decrypts to null");
                        TestAssert.True(tokens.Decrypt("not-valid-base64!!!") == null, "non-base64 decrypts to null");
                        TestAssert.True(tokens.Decrypt("QUJD") == null, "too-short ciphertext decrypts to null");

                        string second = tokens.Encrypt("ses_abc123");
                        TestAssert.True(token != second, "a fresh random IV should make each token distinct");

                        SessionTokenService other = new SessionTokenService("different-key");
                        TestAssert.True(other.Decrypt(token) == null, "a token must not decrypt under a different key");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Reliability", "Token_Encrypt_Null_Throws", "Encrypting a null/empty session id is rejected",
                    ct =>
                    {
                        SessionTokenService tokens = new SessionTokenService("k");
                        bool threw = false;
                        try { tokens.Encrypt(null); } catch (ArgumentNullException) { threw = true; }
                        TestAssert.True(threw, "encrypting null should throw ArgumentNullException");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Reliability", "Password_Hash_Verify", "Password hashing is stable and verification is correct",
                    ct =>
                    {
                        TestAssert.Equal(PasswordHasher.Hash(null), PasswordHasher.Hash(""));
                        string hash = PasswordHasher.Hash("correct horse");
                        TestAssert.True(PasswordHasher.Verify("correct horse", hash), "correct password verifies");
                        TestAssert.True(!PasswordHasher.Verify("wrong", hash), "wrong password fails");
                        TestAssert.True(!PasswordHasher.Verify("correct horse", null), "verify against null hash is false");
                        TestAssert.True(hash.Length == 64, "SHA-256 hex digest is 64 chars");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Reliability", "SecureEquals", "Constant-time compare handles null/length/content",
                    ct =>
                    {
                        TestAssert.True(AuthEvaluator.SecureEquals("abc", "abc"), "equal strings compare true");
                        TestAssert.True(!AuthEvaluator.SecureEquals("abc", "abd"), "different content is false");
                        TestAssert.True(!AuthEvaluator.SecureEquals("abc", "abcd"), "different length is false");
                        TestAssert.True(!AuthEvaluator.SecureEquals(null, "abc"), "null is false");
                        TestAssert.True(!AuthEvaluator.SecureEquals("abc", null), "null is false");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Reliability", "Credential_Keys", "Access/secret keys are prefixed, long, and unique",
                    ct =>
                    {
                        string access = CredentialKeyGenerator.GenerateAccessKey();
                        string secret = CredentialKeyGenerator.GenerateSecretKey();
                        TestAssert.True(access.StartsWith("access_", StringComparison.Ordinal) && access.Length >= 39, "access key is prefixed and long");
                        TestAssert.True(secret.StartsWith("secret_", StringComparison.Ordinal) && secret.Length >= 39, "secret key is prefixed and long");
                        TestAssert.True(access != CredentialKeyGenerator.GenerateAccessKey(), "access keys are unique");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Reliability", "Anonymous_Paths", "Anonymous vs protected path classification",
                    ct =>
                    {
                        TestAssert.True(AuthEvaluator.IsAnonymousPath("/"), "root is anonymous");
                        TestAssert.True(AuthEvaluator.IsAnonymousPath("/health"), "health is anonymous");
                        TestAssert.True(AuthEvaluator.IsAnonymousPath("/swagger/index.html"), "swagger ui is anonymous");
                        TestAssert.True(AuthEvaluator.IsAnonymousPath("/openapi.json"), "openapi doc is anonymous");
                        TestAssert.True(AuthEvaluator.IsAnonymousPath("/v1.0/token"), "login is anonymous");
                        TestAssert.True(!AuthEvaluator.IsAnonymousPath("/api/chat"), "chat is protected");
                        TestAssert.True(!AuthEvaluator.IsAnonymousPath("/v1.0/tenants/x/users"), "management is protected");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Reliability", "HuggingFace_Token_Optional", "HuggingFace client constructs without a token (W5.T2)",
                    ct =>
                    {
                        // A token is only needed for gated/private repos; anonymous construction must not throw.
                        SharpAI.Hosting.HuggingFaceClient anon = new SharpAI.Hosting.HuggingFaceClient(new SyslogLogging.LoggingModule(), null);
                        TestAssert.True(anon != null, "anonymous client should construct");
                        SharpAI.Hosting.HuggingFaceClient empty = new SharpAI.Hosting.HuggingFaceClient(new SyslogLogging.LoggingModule(), "");
                        TestAssert.True(empty != null, "empty-token client should construct");
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("Reliability", "Reliability & fault injection (deterministic)", cases, null, null);
        }

        #endregion
    }
}
