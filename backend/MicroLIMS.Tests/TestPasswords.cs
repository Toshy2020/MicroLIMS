namespace MicroLIMS.Tests;

// BCrypt hashes for seeded test users.
//
// BCrypt.HashPassword defaults to work factor 11 (~100-250 ms per call), and
// Verify re-runs the hash at whatever cost is stored in the hash - so every
// signature or login against a default-cost user pays that price again.
// Tests only need a real, verifiable BCrypt hash, not a slow one: work
// factor 4 (the BCrypt minimum) keeps Verify genuine while costing ~1 ms.
//
// Production code is unaffected - services that hash new passwords
// (UserService, AuthenticationService, AdminPasswordRecoveryService,
// DbSeeder) still use the library default.
public static class TestPasswords
{
    public const int WorkFactor = 4;

    public static string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
}
