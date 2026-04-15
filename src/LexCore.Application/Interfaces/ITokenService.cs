namespace LexCore.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, string role, string name);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(string token, string storedToken);
}
