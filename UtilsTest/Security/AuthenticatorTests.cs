using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Security.Cryptography;
using Utils.Objects;
using Utils.Security;

namespace Utils.Tests.Security
{
    [TestClass]
    public class AuthenticatorTests
    {
        [TestMethod]
        public void ComputeAuthenticator_ShouldReturnValidCode()
        {
            // Arrange
            byte[] key = { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30 };
            Authenticator authenticator = new Authenticator("HMACSHA256", key, 6, 30);

            // Act
            string code = authenticator.ComputeAuthenticator();

            // Assert
            Assert.IsNotNull(code);
            Assert.AreEqual(6, code.Length);
        }

        [TestMethod]
        public void VerifyAuthenticator_ShouldReturnTrueForValidCode()
        {
            // Arrange
            byte[] key = { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30 };
            Authenticator authenticator = new Authenticator("HMACSHA256", key, 6, 30);
            string validCode = authenticator.ComputeAuthenticator();

            // Act
            bool result = authenticator.VerifyAuthenticator(1, validCode);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyAuthenticator_ShouldReturnFalseForInvalidCode()
        {
            // Arrange
            byte[] key = { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30 };
            Authenticator authenticator = new Authenticator("HMACSHA256", key, 6, 30);
            string validCode = authenticator.ComputeAuthenticator();

            // Act
            bool result = authenticator.VerifyAuthenticator(1, "123456"); // An invalid code

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void VerifyAuthenticator_ShouldReturnFalseForCodeWithInvalidLength()
        {
            // Arrange
            byte[] key = { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30 };
            Authenticator authenticator = new Authenticator("HMACSHA256", key, 6, 30);

            // Act
            bool result = authenticator.VerifyAuthenticator(1, "12345");

            // Assert
            Assert.IsFalse(result);
        }
    }
}
