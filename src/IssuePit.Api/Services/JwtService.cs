using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IssuePit.Core.Entities;
using Microsoft.IdentityModel.Tokens;

namespace IssuePit.Api.Services;

/// <summary>
/// Generates and validates JWT tokens used for user authentication sessions.
/// </summary>
public class JwtService(IConfiguration configuration)
{
    private readonly string _secret = !string.IsNullOrEmpty(configuration["Jwt:Secret"])
        ? configuration["Jwt:Secret"]!
        : "issuepit-default-dev-secret-change-in-production";
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "issuepit";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "issuepit";
    private readonly int _expiryDays = int.TryParse(configuration["Jwt:ExpiryDays"], out var d) ? d : 7;

    public string GenerateToken(User user, string sessionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, sessionId),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("username", user.Username),
            new Claim("tenantId", user.TenantId.ToString()),
            new Claim("avatarUrl", user.AvatarUrl ?? string.Empty),
            new Claim("githubId", user.GitHubId ?? string.Empty),
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_expiryDays),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenValidationParameters GetValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = _issuer,
        ValidAudience = _audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
    };
}
