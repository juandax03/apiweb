using Microsoft.AspNetCore.Mvc;
using ProyectoBackendCsharp.Services;

[Route("api/[controller]")]
[ApiController]
public class ControladorUsuarios : ControllerBase
{
    private readonly ControlConexion _controlConexion;

    public ControladorUsuarios(ControlConexion controlConexion)
    {
        _controlConexion = controlConexion ?? throw new ArgumentNullException(nameof(controlConexion));
    }

    [HttpPost("registrar")]
    public IActionResult RegistrarUsuario([FromBody] UsuarioDto usuario)
    {
        if (usuario == null || string.IsNullOrWhiteSpace(usuario.Username) || string.IsNullOrWhiteSpace(usuario.Password) || string.IsNullOrWhiteSpace(usuario.Rol))
        {
            return BadRequest("Todos los campos son obligatorios.");
        }

        bool registroExitoso = _controlConexion.RegistrarUsuario(usuario.Username, usuario.Password, usuario.Rol);
        if (registroExitoso)
        {
            return Ok("Usuario registrado exitosamente.");
        }
        return BadRequest("Error al registrar el usuario.");
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] UsuarioDto usuario)
    {
        try
        {
            _controlConexion.AbrirBd(); // Abre la conexión a la base de datos

            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Username) || string.IsNullOrWhiteSpace(usuario.Password))
            {
                return BadRequest("El nombre de usuario y la contraseña son obligatorios.");
            }

            // Buscar al usuario en la base de datos
            var query = "SELECT * FROM Usuarios WHERE Username = @Username";
            var parametros = new[] { _controlConexion.CreateParameter("@Username", usuario.Username) };
            var resultado = _controlConexion.EjecutarConsultaSql(query, parametros);

            if (resultado.Rows.Count == 0)
            {
                return Unauthorized("Usuario o contraseña inválidos.");
            }

            var usuarioExistente = resultado.Rows[0];
            string passwordHash = usuarioExistente["PasswordHash"].ToString();
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(usuario.Password, passwordHash);
            if (!isPasswordValid)
            {
                return Unauthorized("Usuario o contraseña inválidos.");
            }

            var response = new
            {
                Message = "Inicio de sesión exitoso",
                Rol = usuarioExistente["Rol"].ToString(),
                Token = Guid.NewGuid().ToString()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno del servidor: {ex.Message}");
        }
        finally
        {
            _controlConexion.CerrarBd(); // Cierra la conexión en el bloque finally para asegurar que siempre se cierre
        }
    }
}

public class UsuarioDto
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string? Rol { get; set; } 
}
