using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api.Data;

namespace TaskFlow.Api.Services;

/// <summary>
/// Emite y valida los tokens JWT, y calcula el hash de las contrasenas.
/// </summary>
public interface ITokenService
{
    /// <summary>Genera un token firmado para el usuario indicado.</summary>
    string CrearToken(int usuarioId, string nombre, string email, string rol);

    /// <summary>Calcula el hash con sal de una contrasena en claro.</summary>
    string HashearContrasena(string contrasena);

    /// <summary>Comprueba una contrasena contra su hash almacenado.</summary>
    bool VerificarContrasena(string contrasena, string hash);
}

/// <summary>Implementacion del servicio de tokens con HMAC-SHA256.</summary>
public class TokenService : ITokenService
{
    private readonly SymmetricSecurityKey _clave;
    private readonly string _issuer;
    private readonly string _audience;

    /// <summary>Construye el servicio a partir de la clave de firma configurada.</summary>
    public TokenService(IConfiguration configuracion)
    {
        var secreto = configuracion["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta configurar Jwt:Key");
        _clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secreto));
        _issuer = configuracion["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Falta configurar Jwt:Issuer");
        _audience = configuracion["Jwt:Audience"]
            ?? throw new InvalidOperationException("Falta configurar Jwt:Audience");
    }

    /// <inheritdoc />
    public string CrearToken(int usuarioId, string nombre, string email, string rol)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new(ClaimTypes.Name, nombre),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, rol)
        };

        var credenciales = new SigningCredentials(_clave, SecurityAlgorithms.HmacSha256);

        // El emisor y la audiencia deben viajar en el token: la validacion los
        // exige, y si se omiten aqui la API rechaza su propio token con 401.
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string HashearContrasena(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            contrasena, sal, 100_000, HashAlgorithmName.SHA256, 32);

        return $"{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc />
    public bool VerificarContrasena(string contrasena, string hashAlmacenado)
    {
        var partes = hashAlmacenado.Split('.', 2);
        if (partes.Length != 2)
        {
            return false;
        }

        var sal = Convert.FromBase64String(partes[0]);
        var esperado = Convert.FromBase64String(partes[1]);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(
            contrasena, sal, 100_000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(esperado, calculado);
    }
}
