using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace MYSql;

internal class Connection
{
    private readonly MySqlConnection ConnectionInstane;
    internal bool Used = true;

    public Connection(Database parentDatabase)
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
                using var Command = new MySqlCommand(task.Command, ConnectionInstane);
                if (task.ReaderCallback == null)
                    await Command.ExecuteNonQueryAsync();
                else
                {
                    var reader = await Command.ExecuteReaderAsync();
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