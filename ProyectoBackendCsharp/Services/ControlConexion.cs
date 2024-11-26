#nullable enable // Habilita las características de referencia nula en C#, permitiendo anotaciones y advertencias relacionadas con posibles valores nulos.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;

namespace ProyectoBackendCsharp.Services
{
    public class ControlConexion
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private IDbConnection? _dbConnection;

        public ControlConexion(IWebHostEnvironment env, IConfiguration configuration)
        {
            _env = env; 
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _dbConnection = null;
        }

        // Método para abrir la base de datos, compatible con LocalDB y SQL Server.
        public void AbrirBd()
        {
            try
            {
                string provider = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider not configured.");
                string? connectionString = _configuration.GetConnectionString(provider);

                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string is null or empty.");

                Console.WriteLine($"Attempting to open connection with provider: {provider}");
                Console.WriteLine($"Connection string: {connectionString}");

                switch (provider)
                {
                    case "LocalDb":
                        string appDataPath = Path.Combine(_env.ContentRootPath, "App_Data");
                        AppDomain.CurrentDomain.SetData("DataDirectory", appDataPath);
                        _dbConnection = new SqlConnection(connectionString);
                        break;
                    case "SqlServer":
                        _dbConnection = new SqlConnection(connectionString);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported database provider. Only LocalDb and SqlServer are supported.");
                }

                _dbConnection.Open();
                Console.WriteLine("Database connection opened successfully.");
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"SqlException occurred: {ex.Message}");
                Console.WriteLine($"Error Number: {ex.Number}");
                Console.WriteLine($"Error State: {ex.State}");
                Console.WriteLine($"Error Class: {ex.Class}");
                throw new InvalidOperationException("Failed to open the database connection due to a SQL error.", ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                throw new InvalidOperationException("Failed to open the database connection.", ex);
            }
        }

public bool ActualizarUsuario(int id, string username, string password, string rol)
{
    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password); // Encripta la contraseña
    string query = "UPDATE Usuarios SET Username = @Username, PasswordHash = @PasswordHash, Rol = @Rol WHERE Id = @Id"; // Excluye `Id` del `SET`

    try
    {
        AbrirBd();
        using (SqlCommand comando = new SqlCommand(query, (SqlConnection)_dbConnection))
        {
            comando.Parameters.AddWithValue("@Id", id); // Se usa como filtro en la cláusula `WHERE`
            comando.Parameters.AddWithValue("@Username", username);
            comando.Parameters.AddWithValue("@PasswordHash", hashedPassword);
            comando.Parameters.AddWithValue("@Rol", rol);

            int filasAfectadas = comando.ExecuteNonQuery();
            return filasAfectadas > 0;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Exception occurred: {ex.Message}");
        return false;
    }
    finally
    {
        CerrarBd();
    }
}



        // Método para registrar un usuario
        public bool RegistrarUsuario(string username, string password, string rol)
        {
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            string query = "INSERT INTO Usuarios (Username, PasswordHash, Rol) VALUES (@Username, @PasswordHash, @Rol)";

            try
            {
                AbrirBd();
                using (SqlCommand comando = new SqlCommand(query, (SqlConnection)_dbConnection))
                {
                    comando.Parameters.AddWithValue("@Username", username);
                    comando.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                    comando.Parameters.AddWithValue("@Rol", rol);

                    int filasAfectadas = comando.ExecuteNonQuery();
                    return filasAfectadas > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                return false;
            }
            finally
            {
                CerrarBd();
            }
        }

        // Método para validar inicio de sesión
        public bool ValidarUsuario(string username, string password)
        {
            string query = "SELECT PasswordHash FROM Usuarios WHERE Username = @Username";
            try
            {
                AbrirBd();
                using (SqlCommand comando = new SqlCommand(query, (SqlConnection)_dbConnection))
                {
                    comando.Parameters.AddWithValue("@Username", username);
                    var resultado = comando.ExecuteScalar();
                    if (resultado != null)
                    {
                        string hashedPassword = resultado.ToString();
                        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                return false;
            }
            finally
            {
                CerrarBd();
            }
        }

        // Método específico para abrir una base de datos LocalDB.
        public void AbrirBdLocalDB(string archivoDb)
        {
            try
            {
                string dbFileName = archivoDb.EndsWith(".mdf") ? archivoDb : archivoDb + ".mdf";
                string appDataPath = Path.Combine(_env.ContentRootPath, "App_Data");
                string filePath = Path.Combine(appDataPath, dbFileName);
                string connectionString = $@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename={filePath};Integrated Security=True";

                _dbConnection = new SqlConnection(connectionString);
                _dbConnection.Open();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to open the LocalDB connection.", ex);
            }
        }

        // Método para cerrar la conexión a la base de datos.
        public void CerrarBd()
        {
            try
            {
                if (_dbConnection != null && _dbConnection.State == ConnectionState.Open)
                {
                    _dbConnection.Close();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to close the database connection.", ex);
            }
        }

        // Método para ejecutar un comando SQL y devolver el número de filas afectadas.
        public int EjecutarComandoSql(string consultaSql, DbParameter[] parametros)
        {
            try
            {
                if (_dbConnection == null || _dbConnection.State != ConnectionState.Open)
                    throw new InvalidOperationException("Database connection is not open.");

                using (var comando = _dbConnection.CreateCommand())
                {
                    comando.CommandText = consultaSql;
                    foreach (var parametro in parametros)
                    {
                        Console.WriteLine($"Adding parameter: {parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}");
                        comando.Parameters.Add(parametro);
                    }
                    int filasAfectadas = comando.ExecuteNonQuery();
                    return filasAfectadas;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                throw new InvalidOperationException("Failed to execute SQL command.", ex);
            }
        }

        // Método para ejecutar una consulta SQL y devolver un DataTable con los resultados.
        public DataTable EjecutarConsultaSql(string consultaSql, DbParameter[]? parametros)
        {
            if (_dbConnection == null || _dbConnection.State != ConnectionState.Open)
                throw new InvalidOperationException("Database connection is not open.");

            try
            {
                using (var comando = _dbConnection.CreateCommand())
                {
                    comando.CommandText = consultaSql;
                    if (parametros != null)
                    {
                        foreach (var param in parametros)
                        {
                            Console.WriteLine($"Adding parameter: {param.ParameterName} = {param.Value}, DbType: {param.DbType}");
                            comando.Parameters.Add(param);
                        }
                    }

                    var resultado = new DataSet();
                    var adaptador = new SqlDataAdapter((SqlCommand)comando);

                    Console.WriteLine($"Executing command: {comando.CommandText}");
                    adaptador.Fill(resultado);
                    Console.WriteLine("DataSet filled");

                    if (resultado.Tables.Count == 0)
                    {
                        Console.WriteLine("No tables returned in the DataSet");
                        throw new Exception("No tables returned in the DataSet");
                    }

                    Console.WriteLine($"Number of tables in DataSet: {resultado.Tables.Count}");
                    Console.WriteLine($"Number of rows in first table: {resultado.Tables[0].Rows.Count}");

                    return resultado.Tables[0];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                throw new Exception($"Failed to execute SQL query. Error: {ex.Message}", ex);
            }
        }

        // Método para crear un parámetro de consulta SQL.
        public DbParameter CreateParameter(string name, object? value)
        {
            try
            {
                string provider = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider not configured.");
                return provider switch
                {
                    "SqlServer" => new SqlParameter(name, value ?? DBNull.Value),
                    "LocalDb" => new SqlParameter(name, value ?? DBNull.Value),
                    _ => throw new InvalidOperationException("Unsupported database provider. Only LocalDb and SqlServer are supported."),
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create parameter.", ex);
            }
        }

        // Método para obtener la conexión actual a la base de datos.
        public IDbConnection? GetConnection()
        {
            return _dbConnection;
        }
    }
}