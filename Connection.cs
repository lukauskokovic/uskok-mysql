using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace uskok_mysql;
/// <summary>
/// An open mysql connection
/// </summary>
internal class Connection
{
    /// <summary>
    /// Instance to the mysql connector connection
    /// </summary>
    private readonly MySqlConnection _connectionInstance;
    /// <summary>
    /// Indicates if the connection is in use
    /// </summary>
    internal bool Used;

    internal Connection(Database parentDatabase)
    {
        Used = true;
        _connectionInstance = new MySqlConnection(parentDatabase.ConnectionString);
        _connectionInstance.Open();
        Used = false;
    }

    internal async void HandleTask(MySqlTask task)
    {
        Used = true;
        try
        {
            if (_connectionInstance.State == System.Data.ConnectionState.Closed)
                await _connectionInstance.OpenAsync();
            var Command = new MySqlCommand(task.Command, _connectionInstance);
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
            Debugger.Print($"Error processing {task.Command}: {ex.Message}");
        }
        task.Finished = true;
        Used = false;
    }

    public void Close()
    {
        _connectionInstance.Close();
        _connectionInstance.Dispose();
    }

    public async Task CloseAsync()
    {
        await _connectionInstance.CloseAsync();
        await _connectionInstance.DisposeAsync();
    }
}