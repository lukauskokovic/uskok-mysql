using MySqlConnector;
using System;
using System.Diagnostics;
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

    internal async Task HandleTask(MySqlTask task)
    {
        Used = true;
        MySqlCommand command = null;
        MySqlDataReader reader = null;
        try
        {
            if (_connectionInstance.State == System.Data.ConnectionState.Closed)
                await _connectionInstance.OpenAsync();
            command = new MySqlCommand(task.Command, _connectionInstance);
            if (task.ReaderCallback == null) //Means that we dont expect anything back from the database(insert, replace, update)
                await command.ExecuteNonQueryAsync();
            else
            {
                //Gets the reader
                reader = await command.ExecuteReaderAsync();
                //Awaits the reader callback(reading data)
                await task.ReaderCallback(reader);
            }
        }
        catch (Exception ex)
        {
            if (ex is MySqlException mySqlException)
            {
                if (mySqlException.ErrorCode is MySqlErrorCode.None or MySqlErrorCode.UnableToConnectToHost)
                {
                    Debugger.Print($"Error processing {task.Command}: {ex.Message}, Trying again");
                    if (command != null)
                        await command.DisposeAsync();
                    await HandleTask(task);
                }
            }
            else Debugger.Print($"Error processing {task.Command}: {ex.Message}");

        }
        finally
        {
            if(command != null)
                await command.DisposeAsync();
            if(reader != null)
                await reader.DisposeAsync();
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