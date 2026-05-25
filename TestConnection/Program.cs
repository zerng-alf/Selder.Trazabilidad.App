using System;
using Microsoft.Data.SqlClient;
using Selder.Trazabilidad.App.Configuration;

class Program
{
    static void Main()
    {
        Console.WriteLine("Intentando conectar a SQL Server...");
        Console.WriteLine($"ConnectionString: {AppConfig.ConnectionString}");
        
        try
        {
            using var connection = new SqlConnection(AppConfig.ConnectionString);
            connection.Open();
            Console.WriteLine("✓ Conexión exitosa!");

            // Listar tablas en la base de datos
            var query = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo'";
            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();
            
            Console.WriteLine("\nTablas encontradas:");
            while (reader.Read())
            {
                Console.WriteLine($"  - {reader[0]}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
        }
    }
}