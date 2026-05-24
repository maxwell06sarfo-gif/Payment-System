using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PaymentSystem.Core.Entities;

namespace PaymentSystem.Infrastructure.Services;

public class AuthService
{
    private readonly IConfiguration _configuration;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string HashPassword(string password)
    {
        var saltSize = 16;
        var iterations = 100000;
        var keySize = 32;

        byte[] salt = RandomNumberGenerator.GetBytes(saltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            keySize);

        var combinedBytes = new byte[saltSize + keySize];
        Array.Copy(salt, 0, combinedBytes, 0, saltSize);
        Array.Copy(hash, 0, combinedBytes, saltSize, keySize);

        return Convert.ToBase64String(combinedBytes);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        try
        {
            if (string.IsNullOrEmpty(hashedPassword) || hashedPassword.Length % 4 != 0) 
                return false;
                
            byte[] combinedBytes = Convert.FromBase64String(hashedPassword);
            if (combinedBytes.Length != 48) return false;

            var saltSize = 16;
            var iterations = 100000;
            var keySize = 32;

            byte[] salt = new byte[saltSize];
            byte[] hash = new byte[keySize];

            Array.Copy(combinedBytes, 0, salt, 0, saltSize);
            Array.Copy(combinedBytes, saltSize, hash, 0, keySize);

            byte[] testHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                keySize);

            return CryptographicOperations.FixedTimeEquals(hash, testHash);
        }
        catch
        {
            return false;
        }
    }

    public string GenerateJwtToken(User user)
    {
        var secretKey = _configuration["Jwt:Key"] ?? "SUPER_SECRET_RELIABLE_PAYMENT_SYSTEM_KEY_12345!";
        var issuer = _configuration["Jwt:Issuer"] ?? "PaymentSystem";
        var audience = _configuration["Jwt:Audience"] ?? "PaymentSystemUsers";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
