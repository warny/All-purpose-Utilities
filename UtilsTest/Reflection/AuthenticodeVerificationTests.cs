using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.ProcessIsolation;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates the structured Authenticode result types and <see cref="ProcessIsolationPlatformSecurity"/>
/// revocation policy logic added in item 57.
/// </summary>
[TestClass]
public class AuthenticodeVerificationTests
{
    // ─── Item 57: structured result + explicit revocation policy ─────────────────

    [TestMethod]
    public void IsValid_WhenSignedTrustedAndNotRevoked_IsTrue()
    {
        var result = new AuthenticodeVerificationResult
        {
            IsSigned = true,
            IsChainTrusted = true,
            Revocation = RevocationStatus.Valid,
        };

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void IsValid_WhenRevocationNotChecked_IsTrue()
    {
        // RevocationStatus.NotChecked means no revocation was requested — not the same as "revoked".
        var result = new AuthenticodeVerificationResult
        {
            IsSigned = true,
            IsChainTrusted = true,
            Revocation = RevocationStatus.NotChecked,
        };

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void IsValid_WhenRevocationOffline_IsTrue()
    {
        // Offline means the server was unreachable; the cert is not confirmed revoked.
        var result = new AuthenticodeVerificationResult
        {
            IsSigned = true,
            IsChainTrusted = true,
            Revocation = RevocationStatus.Offline,
        };

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void IsValid_WhenRevoked_IsFalse()
    {
        var result = new AuthenticodeVerificationResult
        {
            IsSigned = true,
            IsChainTrusted = true,
            Revocation = RevocationStatus.Revoked,
        };

        Assert.IsFalse(result.IsValid,
            "A revoked certificate must cause IsValid to be false.");
    }

    [TestMethod]
    public void IsValid_WhenChainNotTrusted_IsFalse()
    {
        var result = new AuthenticodeVerificationResult
        {
            IsSigned = true,
            IsChainTrusted = false,
            Revocation = RevocationStatus.Valid,
        };

        Assert.IsFalse(result.IsValid,
            "An untrusted chain must cause IsValid to be false.");
    }

    [TestMethod]
    public void IsValid_WhenNotSigned_IsFalse()
    {
        var result = new AuthenticodeVerificationResult
        {
            IsSigned = false,
            IsChainTrusted = false,
            Revocation = RevocationStatus.NotChecked,
        };

        Assert.IsFalse(result.IsValid, "An unsigned file must cause IsValid to be false.");
    }

    [TestMethod]
    public void VerifyAuthenticodeSignature_OnNonWindows_ThrowsPlatformNotSupportedException()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test is for non-Windows platforms only.");
            return;
        }

        Assert.ThrowsException<PlatformNotSupportedException>(
            () => ProcessIsolationPlatformSecurity.VerifyAuthenticodeSignature("some-file.dll"));
    }

    [TestMethod]
    public void HasValidAuthenticodeSignature_OnNonWindows_ThrowsPlatformNotSupportedException()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test is for non-Windows platforms only.");
            return;
        }

        Assert.ThrowsException<PlatformNotSupportedException>(
            () => ProcessIsolationPlatformSecurity.HasValidAuthenticodeSignature("some-file.dll"));
    }

    [TestMethod]
    public void VerifyAuthenticodeSignature_FileDoesNotExist_IsNotValid()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Authenticode is Windows-only.");
            return;
        }

        AuthenticodeVerificationResult result = ProcessIsolationPlatformSecurity.VerifyAuthenticodeSignature(
            @"C:\does-not-exist-xyz-12345.dll",
            AuthenticodeRevocationPolicy.None);

        // A non-existent file cannot be trusted. The exact failure path depends on which error
        // code WinVerifyTrust returns for a missing file; we only assert the aggregate IsValid.
        Assert.IsFalse(result.IsValid,
            "IsValid must be false when the file does not exist.");
    }

    [TestMethod]
    public void AuthenticodeRevocationPolicy_DefinesRequiredValues()
    {
        // Guard against regression to WTD_REVOKE_NONE-only behaviour (item 57 fix).
        Assert.IsTrue(Enum.IsDefined(typeof(AuthenticodeRevocationPolicy), AuthenticodeRevocationPolicy.None));
        Assert.IsTrue(Enum.IsDefined(typeof(AuthenticodeRevocationPolicy), AuthenticodeRevocationPolicy.CacheOnly));
        Assert.IsTrue(Enum.IsDefined(typeof(AuthenticodeRevocationPolicy), AuthenticodeRevocationPolicy.Online));
    }
}
