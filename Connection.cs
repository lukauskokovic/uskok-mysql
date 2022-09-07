using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace MYSql;
/// <summary>
/// An open mysql connection
/// </summary>
internal class Connection
{
    /// <summary>
    /// Instance to the mysqlconnector connection
    /// </summary>
    private readonly MySqlConnection ConnectionInstane;
    /// <summary>
    /// Indicates if the connection is in use
    /// </summary>
    internal bool Used = true;

    internal Connection(Database parentDatabase)
    {
        ConnectionInstane = new MySqlConnection(parentDatabase.ConnectionString);
        ConnectionInstane.Open();
        Used = false;
    }

    internal void HandleTask(MYSqlTask task)
    {
        Used = true;
        Task.Run(async () => 
        {
            try
            {
                var Command = new MySqlCommand(task.Command, ConnectionInstane);
                if (task.ReaderCallback == null)//Means that we dont expect anything back from the database(insert, replace, update)
                    await Command.ExecuteNonQueryAsync();
                else
                {
                    //Gets the reader
                    var reader = await Command.ExecuteReaderAsync();
                    //Awaits the reader callback(reading data)
                    await task.ReaderCallback(reader);
                    await reader.DisposeAsync();
                }
                await Command.DisposeAsync();
            } 
            catch(Exception ex)
            {
                Console.WriteLine($"Error processing {task.Command}: {ex.Message}");
            }
            task.Finished = true;
            Used = false;
        });
    }

    public void Close()
    {
        ConnectionInstane.Close();
        ConnectionInstane.Dispose();
    }

    public async Task CloseAsync()
    {
        await ConnectionInstane.CloseAsync();
        await ConnectionInstane.DisposeAsync();
    }
}