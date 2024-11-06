#nullable enable // Habilita las características de referencia nula en C#, permitiendo anotaciones y advertencias relacionadas con posibles valores nulos.
using System; // Importa el espacio de nombres que contiene tipos fundamentales como Exception, Console, etc.
using System.Collections.Generic; // Importa el espacio de nombres para colecciones genéricas como Dictionary.
using System.Data; // Importa el espacio de nombres para clases relacionadas con bases de datos.
using System.Data.Common; // Importa el espacio de nombres que define la clase base para proveedores de datos.
using Microsoft.AspNetCore.Authorization; // Importa el espacio de nombres para el control de autorización en ASP.NET Core.
using Microsoft.AspNetCore.Mvc; // Importa el espacio de nombres para la creación de controladores en ASP.NET Core.
using Microsoft.Extensions.Configuration; // Importa el espacio de nombres para acceder a la configuración de la aplicación.
using Microsoft.Data.SqlClient; // Importa el espacio de nombres necesario para trabajar con SQL Server y LocalDB.
using System.Linq; // Importa el espacio de nombres para operaciones de consulta con LINQ.
using System.Text.Json; // Importa el espacio de nombres para manejar JSON.
using ProyectoBackendCsharp.Models; // Importa los modelos del proyecto.
using ProyectoBackendCsharp.Services; // Importa los servicios del proyecto.
using BCrypt.Net; // Importa el espacio de nombres para trabajar con BCrypt para hashing de contraseñas.

namespace ProyectoBackendCsharp.Controllers
{
    [Route("api/{projectName}/{tableName}")] // Define la ruta de la API para este controlador.
    [ApiController] // Indica que esta clase es un controlador de API.
    [Authorize] // Requiere autorización para acceder a los métodos de este controlador.
    [AllowAnonymous]
    public class EntidadesController : ControllerBase // Define un controlador llamado `EntidadesController`.
    {
        private readonly ControlConexion controlConexion; // Declara una instancia del servicio ControlConexion.
        private readonly IConfiguration _configuration; // Declara una instancia de la configuración de la aplicación.

        // Constructor que recibe las dependencias necesarias y lanza excepciones si son nulas.
        public EntidadesController(ControlConexion controlConexion, IConfiguration configuration)
        {
            this.controlConexion = controlConexion ?? throw new ArgumentNullException(nameof(controlConexion));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpGet] // Define una ruta HTTP GET para este método.
        public IActionResult Listar(string projectName, string tableName) // Método que lista todas las filas de una tabla dada.
        {
            if (string.IsNullOrWhiteSpace(tableName)) // Verifica si el nombre de la tabla está vacío o solo contiene espacios en blanco.
                return BadRequest("El nombre de la tabla no puede estar vacío."); // Retorna una respuesta de error si la tabla está vacía.

            try
            {
                var lista = new List<Dictionary<string, object?>>(); // Crea una lista para almacenar las filas resultantes.
                string comandoSQL = $"SELECT * FROM {tableName}"; // Define el comando SQL para seleccionar todas las filas de la tabla.

                controlConexion.AbrirBd(); // Abre la conexión a la base de datos.
                var tabla = controlConexion.EjecutarConsultaSql(comandoSQL, null); // Ejecuta la consulta SQL y almacena el resultado en un DataTable.
                controlConexion.CerrarBd(); // Cierra la conexión a la base de datos.

                foreach (DataRow fila in tabla.Rows) // Recorre cada fila en el DataTable.
                {
                    var propiedades = fila.Table.Columns.Cast<DataColumn>()
                                        .ToDictionary(col => col.ColumnName, col => fila[col] == DBNull.Value ? null : fila[col]); // Convierte cada fila en un diccionario.
                    lista.Add(propiedades); // Agrega el diccionario a la lista.
                }

                return Ok(lista); // Retorna la lista de filas en formato JSON.
            }
            catch (Exception ex) // Captura cualquier excepción que ocurra durante la ejecución.
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna una respuesta de error 500 con el mensaje de la excepción.
            }
        }

        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpGet("{keyName}/{value}")] // Define una ruta HTTP GET con parámetros adicionales.
        public IActionResult GetByKey(string projectName, string tableName, string keyName, string value) // Método que obtiene una fila específica basada en una clave.
        {
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(keyName) || string.IsNullOrWhiteSpace(value)) // Verifica si alguno de los parámetros está vacío.
            {
                return BadRequest("El nombre de la tabla, el nombre de la clave y el valor no pueden estar vacíos."); // Retorna una respuesta de error si algún parámetro está vacío.
            }

            controlConexion.AbrirBd(); // Abre la conexión a la base de datos.
            try
            {
                string provider = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider not configured."); // Obtiene el proveedor de base de datos desde la configuración.
                
                string query;
                DbParameter[] parameters;
                
                // Define la consulta SQL y los parámetros para SQL Server y LocalDB.
                query = "SELECT data_type FROM information_schema.columns WHERE table_name = @tableName AND column_name = @columnName";
                parameters = new DbParameter[]
                {
                    CreateParameter("@tableName", tableName),
                    CreateParameter("@columnName", keyName)
                };

                Console.WriteLine($"Executing SQL query: {query} with parameters: tableName={tableName}, columnName={keyName}");

                var dataTypeResult = controlConexion.EjecutarConsultaSql(query, parameters); // Ejecuta la consulta SQL para determinar el tipo de dato de la clave.

                if (dataTypeResult == null || dataTypeResult.Rows.Count == 0 || dataTypeResult.Rows[0]["data_type"] == DBNull.Value) // Verifica si se obtuvo un resultado válido.
                {
                    return NotFound("No se pudo determinar el tipo de dato."); // Retorna una respuesta de error si no se pudo determinar el tipo de dato.
                }

                string dataType = dataTypeResult.Rows[0]["data_type"]?.ToString() ?? ""; // Obtiene el tipo de dato de la columna.
                Console.WriteLine($"Detected data type for column {keyName}: {dataType}");

                if (string.IsNullOrEmpty(dataType)) // Verifica si el tipo de dato es válido.
                {
                    return NotFound("No se pudo determinar el tipo de dato."); // Retorna una respuesta de error si el tipo de dato es inválido.
                }

                object convertedValue;
                string comandoSQL;

                // Determina cómo tratar el valor y la consulta SQL según el tipo de dato, compatible con SQL Server y LocalDB.
                switch (dataType.ToLower())
                {
                    case "int":
                    case "bigint":
                    case "smallint":
                    case "tinyint":
                        if (int.TryParse(value, out int intValue))
                        {
                            convertedValue = intValue;
                            comandoSQL = $"SELECT * FROM {tableName} WHERE {keyName} = @Value";
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos entero.");
                        }
                        break;
                    case "decimal":
                    case "numeric":
                    case "money":
                    case "smallmoney":
                        if (decimal.TryParse(value, out decimal decimalValue))
                        {
                            convertedValue = decimalValue;
                            comandoSQL = $"SELECT * FROM {tableName} WHERE {keyName} = @Value";
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos decimal.");
                        }
                        break;
                    case "bit":
                        if (bool.TryParse(value, out bool boolValue))
                        {
                            convertedValue = boolValue;
                            comandoSQL = $"SELECT * FROM {tableName} WHERE {keyName} = @Value";
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos booleano.");
                        }
                        break;
                    case "float":
                    case "real":
                        if (double.TryParse(value, out double doubleValue))
                        {
                            convertedValue = doubleValue;
                            comandoSQL = $"SELECT * FROM {tableName} WHERE {keyName} = @Value";
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos flotante.");
                        }
                        break;
                    case "nvarchar":
                    case "varchar":
                    case "nchar":
                    case "char":
                    case "text":
                        convertedValue = value;
                        comandoSQL = $"SELECT * FROM {tableName} WHERE {keyName} = @Value";
                        break;
                    case "date":
                    case "datetime":
                    case "datetime2":
                    case "smalldatetime":
                        if (DateTime.TryParse(value, out DateTime dateValue))
                        {
                            comandoSQL = $"SELECT * FROM {tableName} WHERE CAST({keyName} AS DATE) = @Value";
                            convertedValue = dateValue.Date;
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos fecha.");
                        }
                        break;
                    default:
                        return BadRequest($"Tipo de dato no soportado: {dataType}"); // Retorna un error si el tipo de dato no es soportado.
                }

                var parametro = CreateParameter("@Value", convertedValue); // Crea el parámetro para la consulta SQL.

                Console.WriteLine($"Executing SQL query: {comandoSQL} with parameter: {parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}");

                var resultado = controlConexion.EjecutarConsultaSql(comandoSQL, new DbParameter[] { parametro }); // Ejecuta la consulta SQL con el parámetro.

                Console.WriteLine($"DataSet fill completed for query: {comandoSQL}");

                if (resultado.Rows.Count > 0) // Verifica si hay filas en el resultado.
                {
                    var lista = new List<Dictionary<string, object?>>();
                    foreach (DataRow fila in resultado.Rows)
                    {
                        var propiedades = resultado.Columns.Cast<DataColumn>()
                                           .ToDictionary(col => col.ColumnName, col => fila[col] == DBNull.Value ? null : fila[col]);
                        lista.Add(propiedades);
                    }

                    return Ok(lista); // Retorna las filas encontradas en formato JSON.
                }

                return NotFound(); // Retorna un error 404 si no se encontraron filas.
            }
            catch (Exception ex) // Captura cualquier excepción que ocurra durante la ejecución.
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna un error 500 si ocurre una excepción.
            }
            finally
            {
                controlConexion.CerrarBd(); // Cierra la conexión a la base de datos.
            }
        }

        // Método privado para convertir un JsonElement en su tipo correspondiente.
        private object? ConvertJsonElement(JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Null)
                return null;

            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    return DateTime.TryParse(jsonElement.GetString(), out DateTime dateValue) ? (object)dateValue : jsonElement.GetString();
                case JsonValueKind.Number:
                    return jsonElement.TryGetInt32(out var intValue) ? (object)intValue : jsonElement.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Object:
                    return jsonElement.GetRawText();
                case JsonValueKind.Array:
                    return jsonElement.GetRawText();
                default:
                    throw new InvalidOperationException($"Unsupported JsonValueKind: {jsonElement.ValueKind}");
            }
        }

        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpPost] // Define una ruta HTTP POST para este método.
        public IActionResult Crear(string projectName, string tableName, [FromBody] Dictionary<string, object?> entidadData)  // Crea una nueva fila en la tabla especificada.
        {
            if (string.IsNullOrWhiteSpace(tableName) || entidadData == null || !entidadData.Any())  // Verifica si el nombre de la tabla o los datos están vacíos.
                return BadRequest("El nombre de la tabla y los datos de la entidad no pueden estar vacíos.");  // Retorna un error si algún parámetro está vacío.

            try
            {
                var propiedades = entidadData.ToDictionary(  // Convierte los datos de la entidad en un diccionario de propiedades.
                    kvp => kvp.Key,
                    kvp => kvp.Value is JsonElement jsonElement ? ConvertJsonElement(jsonElement) : kvp.Value);

                // Verifica si hay un campo de contraseña en los datos, y si lo hay, lo hashea.
                var passwordKeys = new[] { "password", "contrasena", "passw" };  // Lista de posibles nombres para campos de contraseña.
                var passwordKey = propiedades.Keys.FirstOrDefault(k => passwordKeys.Any(pk => k.IndexOf(pk, StringComparison.OrdinalIgnoreCase) >= 0));  // Busca si alguno de los campos es una contraseña.
                
                if (passwordKey != null)  // Si se encontró un campo de contraseña.
                {
                    var plainPassword = propiedades[passwordKey]?.ToString();  // Obtiene el valor de la contraseña.
                    if (!string.IsNullOrEmpty(plainPassword))  // Si la contraseña no está vacía.
                    {
                        propiedades[passwordKey] = BCrypt.Net.BCrypt.HashPassword(plainPassword);  // Hashea la contraseña.
                    }
                }

                string provider = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider not configured.");  // Obtiene el proveedor de base de datos.
                var columnas = string.Join(",", propiedades.Keys);  // Une los nombres de las columnas en una cadena.
                var valores = string.Join(",", propiedades.Keys.Select(k => $"{GetParameterPrefix(provider)}{k}"));  // Une los nombres de los valores en una cadena con su prefijo.
                string comandoSQL = $"INSERT INTO {tableName} ({columnas}) VALUES ({valores})";  // Crea la consulta SQL para insertar una nueva fila.

                var parametros = propiedades.Select(p => CreateParameter($"{GetParameterPrefix(provider)}{p.Key}", p.Value)).ToArray();  // Crea los parámetros para la consulta SQL.

                Console.WriteLine($"Executing SQL query: {comandoSQL} with parameters:");  // Muestra la consulta SQL y los parámetros en la consola.
                foreach (var parametro in parametros)  // Recorre cada parámetro.
                {
                    Console.WriteLine($"{parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}");  // Muestra el nombre y valor del parámetro en la consola.
                }

                controlConexion.AbrirBd();  // Abre la conexión a la base de datos.
                controlConexion.EjecutarComandoSql(comandoSQL, parametros);  // Ejecuta la consulta SQL para insertar la nueva fila.
                controlConexion.CerrarBd();  // Cierra la conexión a la base de datos.

                return Ok("Entidad creada exitosamente.");  // Retorna una respuesta de éxito.
            }
            catch (Exception ex)  // Captura cualquier excepción que ocurra durante la ejecución.
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");  // Muestra el mensaje de la excepción en la consola.
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");  // Retorna un error 500 si ocurre una excepción.
            }
        }

        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpPut("{keyName}/{keyValue}")] // Define una ruta HTTP PUT con parámetros adicionales.
        public IActionResult Actualizar(string projectName, string tableName, string keyName, string keyValue, [FromBody] Dictionary<string, object?> entidadData) // Actualiza una fila en la tabla basada en una clave.
        {
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(keyName) || entidadData == null || !entidadData.Any()) // Verifica si alguno de los parámetros está vacío.
                return BadRequest("El nombre de la tabla, el nombre de la clave y los datos de la entidad no pueden estar vacíos."); // Retorna un error si algún parámetro está vacío.

            try
            {
                var propiedades = entidadData.ToDictionary( // Convierte los datos de la entidad en un diccionario de propiedades.
                    kvp => kvp.Key,
                    kvp => kvp.Value is JsonElement jsonElement ? ConvertJsonElement(jsonElement) : kvp.Value);

                // Verifica si hay un campo de contraseña en los datos, y si lo hay, lo hashea.
                var passwordKeys = new[] { "password", "contrasena", "passw" }; // Lista de posibles nombres para campos de contraseña.
                var passwordKey = propiedades.Keys.FirstOrDefault(k => passwordKeys.Any(pk => k.IndexOf(pk, StringComparison.OrdinalIgnoreCase) >= 0)); // Busca si alguno de los campos es una contraseña.
                
                if (passwordKey != null) // Si se encontró un campo de contraseña.
                {
                    var plainPassword = propiedades[passwordKey]?.ToString(); // Obtiene el valor de la contraseña.
                    if (!string.IsNullOrEmpty(plainPassword)) // Si la contraseña no está vacía.
                    {
                        propiedades[passwordKey] = BCrypt.Net.BCrypt.HashPassword(plainPassword); // Hashea la contraseña.
                    }
                }

                string provider = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider not configured."); // Obtiene el proveedor de base de datos.
                var actualizaciones = string.Join(",", propiedades.Select(p => $"{p.Key}={GetParameterPrefix(provider)}{p.Key}")); // Crea la cadena de actualizaciones para la consulta SQL.
                string comandoSQL = $"UPDATE {tableName} SET {actualizaciones} WHERE {keyName}={GetParameterPrefix(provider)}KeyValue"; // Crea la consulta SQL para actualizar la fila.

                var parametros = propiedades.Select(p => CreateParameter($"{GetParameterPrefix(provider)}{p.Key}", p.Value)).ToList(); // Crea los parámetros para la consulta SQL.
                parametros.Add(CreateParameter($"{GetParameterPrefix(provider)}KeyValue", keyValue)); // Agrega el parámetro para la clave de la fila a actualizar.

                Console.WriteLine($"Executing SQL query: {comandoSQL} with parameters:"); // Muestra la consulta SQL y los parámetros en la consola.
                foreach (var parametro in parametros) // Recorre cada parámetro.
                {
                    Console.WriteLine($"{parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}"); // Muestra el nombre y valor del parámetro en la consola.
                }

                controlConexion.AbrirBd(); // Abre la conexión a la base de datos.
                controlConexion.EjecutarComandoSql(comandoSQL, parametros.ToArray()); // Ejecuta la consulta SQL para actualizar la fila.
                controlConexion.CerrarBd(); // Cierra la conexión a la base de datos.

                return Ok("Entidad actualizada exitosamente."); // Retorna una respuesta de éxito.
            }
            catch (Exception ex) // Captura cualquier excepción que ocurra durante la ejecución.
            {
                Console.WriteLine($"Exception occurred: {ex.Message}"); // Muestra el mensaje de la excepción en la consola.
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna un error 500 si ocurre una excepción.
            }
        }

        // Método privado para obtener el prefijo adecuado para los parámetros SQL, según el proveedor de la base de datos.
        private string GetParameterPrefix(string provider)
        {
            return "@"; // Para SQL Server y LocalDB, el prefijo es "@".
        }

        [AllowAnonymous]
        [HttpGet("listar-entidades")]
        public IActionResult ListarEntidades()
        {
            try
            {
                // Lista de las 18 tablas que pertenecen a tu módulo
                var tablasModulo = new List<string>
                {
                    "ac_proyecto", "tipo_producto", "termino_clave", "docente_producto", 
                    "producto", "desarrolla", "aliado_proyecto", "proyecto", 
                    "palabras_clave", "proyecto_linea", "ods_proyecto", "aa_proyecto", 
                    "area_conocimiento", "objetivo_desarrollo_sostenible", 
                    "area_aplicacion", "docente", "aliado", "linea_investigacion"
                };

                // Consulta las tablas disponibles en la base de datos
                string query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                controlConexion.AbrirBd();
                var resultado = controlConexion.EjecutarConsultaSql(query, null);
                controlConexion.CerrarBd();

                // Filtra las tablas para mostrar solo las del módulo
                var entidades = resultado.Rows.Cast<DataRow>()
                    .Select(row => row["TABLE_NAME"].ToString())
                    .Where(tabla => tablasModulo.Contains(tabla))  // Aquí se filtra la lista
                    .ToList();

                return Ok(entidades);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpDelete("{keyName}/{keyValue}")] // Define una ruta HTTP DELETE con parámetros adicionales.
        public IActionResult Eliminar(string projectName, string tableName, string keyName, string keyValue) // Elimina una fila de la tabla basada en una clave.
        {
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(keyName)) // Verifica si alguno de los parámetros está vacío.
                return BadRequest("El nombre de la tabla o el nombre de la clave no pueden estar vacíos."); // Retorna un error si algún parámetro está vacío.

            try
            {
                string provider = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider not configured."); // Obtiene el proveedor de base de datos.
                string comandoSQL = $"DELETE FROM {tableName} WHERE {keyName}=@KeyValue"; // Crea la consulta SQL para eliminar la fila.
                var parametro = CreateParameter("@KeyValue", keyValue); // Crea el parámetro para la clave de la fila a eliminar.

                controlConexion.AbrirBd(); // Abre la conexión a la base de datos.
                controlConexion.EjecutarComandoSql(comandoSQL, new[] { parametro }); // Ejecuta la consulta SQL para eliminar la fila.
                controlConexion.CerrarBd(); // Cierra la conexión a la base de datos.

                return Ok("Entidad eliminada exitosamente."); // Retorna una respuesta de éxito.
            }
            catch (Exception ex) // Captura cualquier excepción que ocurra durante la ejecución.
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna un error 500 si ocurre una excepción.
            }
        }

        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpGet("/")] // Define una ruta HTTP GET en la raíz de la API.
        public IActionResult GetRoot() // Método que retorna un mensaje indicando que la API está en funcionamiento.
        {
            return Ok("API is running"); // Retorna un mensaje indicando que la API está en funcionamiento.
        }


        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpPost("verificar-contrasena")] // Define una ruta HTTP POST para verificar contraseñas.
        public IActionResult VerificarContrasena(string projectName, string tableName, [FromBody] Dictionary<string, string> datos) // Verifica si la contraseña proporcionada coincide con la almacenada.
        {
            if (string.IsNullOrWhiteSpace(tableName) || datos == null || !datos.ContainsKey("userField") || !datos.ContainsKey("passwordField") || !datos.ContainsKey("userValue") || !datos.ContainsKey("passwordValue")) // Verifica si alguno de los parámetros está vacío.
                return BadRequest("El nombre de la tabla, el campo de usuario, el campo de contraseña, el valor de usuario y el valor de contraseña no pueden estar vacíos."); // Retorna un error si algún parámetro está vacío.

            try
            {
                string userField = datos["userField"]; // Obtiene el nombre del campo de usuario.
                string passwordField = datos["passwordField"]; // Obtiene el nombre del campo de contraseña.
                string userValue = datos["userValue"]; // Obtiene el valor del usuario.
                string passwordValue = datos["passwordValue"]; // Obtiene el valor de la contraseña.

                string provider = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider not configured."); // Obtiene el proveedor de base de datos.
                string comandoSQL = $"SELECT {passwordField} FROM {tableName} WHERE {userField} = @UserValue"; // Crea la consulta SQL para obtener la contraseña almacenada.
                var parametro = CreateParameter("@UserValue", userValue); // Crea el parámetro para el valor del usuario.

                controlConexion.AbrirBd(); // Abre la conexión a la base de datos.
                var resultado = controlConexion.EjecutarConsultaSql(comandoSQL, new DbParameter[] { parametro }); // Ejecuta la consulta SQL para obtener la contraseña.
                controlConexion.CerrarBd(); // Cierra la conexión a la base de datos.

                if (resultado.Rows.Count == 0) // Verifica si no se encontró el usuario.
                {
                    return NotFound("Usuario no encontrado."); // Retorna un error 404 si no se encontró el usuario.
                }

                string hashedPassword = resultado.Rows[0][passwordField]?.ToString() ?? string.Empty; // Obtiene la contraseña hasheada almacenada.

                // Verifica si el hash de la contraseña es válido.
                if (!hashedPassword.StartsWith("$2"))
                {
                    throw new InvalidOperationException("Stored password hash is not a valid BCrypt hash."); // Lanza una excepción si el hash almacenado no es válido.
                }

                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(passwordValue, hashedPassword); // Verifica si la contraseña proporcionada coincide con el hash almacenado.

                if (isPasswordValid) // Si la contraseña es válida.
                {
                    return Ok("Contraseña verificada exitosamente."); // Retorna una respuesta de éxito.
                }
                else // Si la contraseña no es válida.
                {
                    return Unauthorized("Contraseña incorrecta."); // Retorna un error 401 si la contraseña es incorrecta.
                }
            }
            catch (Exception ex) // Captura cualquier excepción que ocurra durante la ejecución.
            {
                Console.WriteLine($"Exception occurred: {ex.Message}"); // Muestra el mensaje de la excepción en la consola.
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna un error 500 si ocurre una excepción.
            }
        }

        // Método para crear un parámetro de consulta SQL basado en el proveedor de base de datos.
        public DbParameter CreateParameter(string name, object? value)
        {
            return new SqlParameter(name, value ?? DBNull.Value); // Crea un parámetro para SQL Server y LocalDB.
        }




[AllowAnonymous]
[HttpPost("consulta-compleja")]
public IActionResult ConsultaCompleja(string projectName, [FromBody] JsonElement criterios)
{
    if (criterios.ValueKind == JsonValueKind.Undefined)
        return BadRequest("Los criterios de consulta no pueden estar vacíos.");

    try
    {
        if (!criterios.TryGetProperty("TablaPrincipal", out var tablaPrincipalElement))
            return BadRequest("Debe especificar una tabla principal.");

string tablaPrincipal = tablaPrincipalElement.TryGetProperty("Nombre", out var nombreProperty) 
    ? nombreProperty.GetString() ?? throw new ArgumentException("El nombre de la tabla principal no puede ser nulo.")
    : throw new ArgumentException("La propiedad 'Nombre' es requerida para la tabla principal.");

string aliasPrincipal = tablaPrincipalElement.TryGetProperty("Alias", out var aliasProperty)
    ? aliasProperty.GetString() ?? tablaPrincipal // Si no se proporciona un alias, usamos el nombre de la tabla
    : tablaPrincipal; // Si la propiedad 'Alias' no existe, usamos el nombre de la tabla

        if (!criterios.TryGetProperty("CamposSeleccionados", out var camposSeleccionadosElement) || camposSeleccionadosElement.GetArrayLength() == 0)
            return BadRequest("Debe seleccionar al menos un campo para la consulta.");

        var camposSeleccionados = camposSeleccionadosElement.EnumerateArray().Select(e => e.GetString()).ToList();
        string camposSeleccionadosStr = string.Join(", ", camposSeleccionados);

        var joinClauses = new List<string>();
        var whereClauses = new List<string>();
        var havingClauses = new List<string>();
        var parameters = new List<DbParameter>();

        string comandoSQL = $"SELECT {camposSeleccionadosStr} FROM {tablaPrincipal} {aliasPrincipal}";

        // Procesar JOINs
        if (criterios.TryGetProperty("Joins", out var joinsElement))
        {
            foreach (var join in joinsElement.EnumerateArray())
            {
                string? tablaSecundaria = join.GetProperty("TablaSecundaria").GetString();
                string? tipoJoin = join.GetProperty("TipoJoin").GetString();
                string? condicionJoin = join.GetProperty("CondicionJoin").GetString();

                if (!string.IsNullOrWhiteSpace(tablaSecundaria) && !string.IsNullOrWhiteSpace(condicionJoin))
                {
                    joinClauses.Add($"{tipoJoin} JOIN {tablaSecundaria} ON {condicionJoin}");
                }
            }
        }

        // Procesar WHERE
        if (criterios.TryGetProperty("CondicionesWhere", out var whereElement))
        {
            for (int i = 0; i < whereElement.GetArrayLength(); i++)
            {
                var condicion = whereElement[i];
                string? campo = condicion.GetProperty("Campo").GetString();
                string? operador = condicion.GetProperty("Operador").GetString();
                var valor = condicion.GetProperty("Valor");

                if (!string.IsNullOrWhiteSpace(campo) && !string.IsNullOrWhiteSpace(operador))
                {
                    if (operador.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase) && valor.ValueKind == JsonValueKind.Array && valor.GetArrayLength() == 2)
                    {
                        var valores = valor.EnumerateArray().ToList();
                        string paramInicio = $"@Where{i}_Start";
                        string paramFin = $"@Where{i}_End";
                        whereClauses.Add($"{campo} {operador} {paramInicio} AND {paramFin}");
                        parameters.Add(CreateParameter(paramInicio, valores[0].GetString()));
                        parameters.Add(CreateParameter(paramFin, valores[1].GetString()));
                    }
                    else if (operador.Equals("IS NULL", StringComparison.OrdinalIgnoreCase) || operador.Equals("IS NOT NULL", StringComparison.OrdinalIgnoreCase))
                    {
                        whereClauses.Add($"{campo} {operador}");
                    }
                    else
                    {
                        string paramName = $"@Where{i}";
                        whereClauses.Add($"{campo} {operador} {paramName}");
                        parameters.Add(CreateParameter(paramName, valor.GetRawText()));
                    }
                }
            }
        }

        // Procesar JOIN antes del GROUP BY
        if (joinClauses.Any())
        {
            comandoSQL += " " + string.Join(" ", joinClauses);
        }

        // Procesar GROUP BY
        if (criterios.TryGetProperty("CamposAgrupacion", out var groupByElement))
        {
            var camposAgrupacion = groupByElement.EnumerateArray().Select(e => e.GetString()).ToList();
            if (camposAgrupacion.Any())
            {
                comandoSQL += $" GROUP BY {string.Join(", ", camposAgrupacion)}";
            }
        }

        // Procesar HAVING
        if (criterios.TryGetProperty("CondicionesHaving", out var havingElement))
        {
            for (int i = 0; i < havingElement.GetArrayLength(); i++)
            {
                var condicion = havingElement[i];
                string? campo = condicion.GetProperty("Campo").GetString();
                string? operador = condicion.GetProperty("Operador").GetString();
                var valor = condicion.GetProperty("Valor");

                if (!string.IsNullOrWhiteSpace(campo) && !string.IsNullOrWhiteSpace(operador))
                {
                    string paramName = $"@Having{i}";
                    havingClauses.Add($"{campo} {operador} {paramName}");
                    parameters.Add(CreateParameter(paramName, valor.GetRawText()));
                }
            }
        }

        // Aplicar la cláusula HAVING
        if (havingClauses.Any())
        {
            comandoSQL += " HAVING " + string.Join(" AND ", havingClauses);
        }

        // Procesar WHERE
        if (whereClauses.Any())
        {
            comandoSQL += " WHERE " + string.Join(" AND ", whereClauses);
        }

        Console.WriteLine($"Executing complex SQL query: {comandoSQL}");
        foreach (var param in parameters)
        {
            Console.WriteLine($"Parameter: {param.ParameterName} = {param.Value}");
        }

        controlConexion.AbrirBd();
        var resultado = controlConexion.EjecutarConsultaSql(comandoSQL, parameters.ToArray());
        controlConexion.CerrarBd();

        if (resultado.Rows.Count == 0)
        {
            return NotFound("No se encontraron resultados para la consulta.");
        }

        var lista = new List<Dictionary<string, object?>>();
        foreach (DataRow fila in resultado.Rows)
        {
            var propiedades = resultado.Columns.Cast<DataColumn>()
                               .ToDictionary(col => col.ColumnName, col => fila[col] == DBNull.Value ? null : fila[col]);
            lista.Add(propiedades);
        }

        return Ok(lista);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Exception occurred: {ex.Message}");
        return StatusCode(500, $"Error interno del servidor: {ex.Message}");
    }
}




[AllowAnonymous]
[HttpPost("ejecutar-procedimiento/{procedureName}")]
public IActionResult EjecutarProcedimientoAlmacenado(string procedureName, [FromBody] JsonElement body)
{
    // Verificar que el nombre del procedimiento no esté vacío
    if (string.IsNullOrWhiteSpace(procedureName))
    {
        return BadRequest(new { Mensaje = "El nombre del procedimiento es requerido." });
    }

    try
    {
        // Abrir la conexión a la base de datos
        controlConexion.AbrirBd();

        // Obtener la conexión
        var connection = controlConexion.GetConnection();
        if (connection == null || connection.State != ConnectionState.Open)
        {
            return StatusCode(500, "No se pudo obtener una conexión válida a la base de datos.");
        }

        using (var command = new SqlCommand(procedureName, (SqlConnection)connection))
        {
            command.CommandType = CommandType.StoredProcedure;

            // Agregar parámetros al comando
            foreach (var property in body.EnumerateObject())
            {
                string paramName = property.Name.StartsWith("@") ? property.Name : "@" + property.Name;
                if (property.Name.EndsWith("productos") && property.Value.ValueKind == JsonValueKind.Array)
                {
                    var productosJson = JsonSerializer.Serialize(property.Value);
                    command.Parameters.AddWithValue(paramName, productosJson);
                }
                else
                {
                    command.Parameters.AddWithValue(paramName, property.Value.GetRawText().Trim('"'));
                }
            }

            // Ejecutar el procedimiento almacenado
            int filasAfectadas = command.ExecuteNonQuery();
            controlConexion.CerrarBd(); // Cerrar la conexión a la base de datos

            return Ok(new { Mensaje = "Procedimiento almacenado ejecutado exitosamente.", FilasAfectadas = filasAfectadas });
        }
    }
    catch (SqlException sqlEx)
    {
        controlConexion.CerrarBd(); // Asegura cerrar la conexión en caso de error
        Console.WriteLine($"SQL Error: {sqlEx.Message}");
        return StatusCode(500, new { Mensaje = "Error en la base de datos.", Detalle = sqlEx.Message });
    }
    catch (Exception ex)
    {
        controlConexion.CerrarBd(); // Asegura cerrar la conexión en caso de error general
        Console.WriteLine($"Error: {ex.Message}");
        return StatusCode(500, new { Mensaje = "Error en el servidor.", Detalle = ex.Message });
    }
}




    }
}
/*
modos de uso
get
http://localhost:5184/api/proyecto/usuario
http://localhost:5184/api/proyecto/usuario/email/admin@empresa.com

post
http://localhost:5184/api/proyecto/usuario/
{
    "email": "nuevo.nuevo@empresa.com",
    "contrasena": "123"
}

put
http://localhost:5184/api/proyecto/usuario/email/nuevo.nuevo@empresa.com
{
    "contrasena": "456"
}

delete
http://localhost:5184/api/proyecto/usuario/email/nuevo.nuevo@empresa.com


200 OK: la solicitud fue exitosa y la respuesta del servidor dependerá del método HTTP utilizado en la solicitud.
201 Creado: la solicitud fue exitosa y se creó un nuevo recurso como resultado.
204 Sin contenido: el servidor procesó con éxito la solicitud y no hay más contenido para enviar en la respuesta.
400 Solicitud incorrecta: el servidor no pudo entender la solicitud debido a una sintaxis inválida.
401 No autorizado: el cliente debe autenticarse para obtener la respuesta solicitada.
403 Prohibido: el cliente no tiene derechos de acceso al contenido.
404 No encontrado: el servidor no puede encontrar el recurso solicitado.
500 Error interno del servidor: el servidor ha encontrado una situación que no sabe cómo manejar.
*/  