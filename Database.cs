using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MYSql;

namespace uskok_mysql;
public class Database : IDisposable
{
    internal readonly string ConnectionString;
    public readonly MYSqlParser Parser;
    private readonly Connection[] Connections;
    private readonly Thread LoopThread;
    private readonly ConcurrentQueue<MySqlTask> TaskQueue = new();

    /// <summary>
    /// Creates a database instance
    /// </summary>
    /// <param name="connectionString">Connection string for mysql database</param>
    /// <param name="connections">How many connections are open at once in the pool(5 is default)</param>
    /// <param name="customConversion">Custom conversion for specific types</param>
    /// <param name="customReadings">Custom readings for a type</param>
    /// <param name="illegalChars">Illegal characters</param>
    public Database(string connectionString, int connections = 5, Dictionary<Type, Func<object, object>> customConversion = null, Dictionary<Type, SQLCustomConversion> customReadings = null, HashSet<char> illegalChars = null)
    {
        ConnectionString = connectionString;
        Parser = new MYSqlParser(customConversion, customReadings, illegalChars);
        Connections = new Connection[connections];
        LoopThread = new Thread(ThreadMethod);
        LoopThread.Start();
    }
    private const int ReconnectTimer = 2000;
    private async void ThreadMethod()
    {
        try
        {
            while (true)
            {
                try
                {
                    InitConnections();
                    Debugger.Print("MYSQL connections opened");
                    break;
                }
                catch (Exception ex)
                {
                    Debugger.Print($"Could not initialize mysql connections '{ex.Message}' trying again in {ReconnectTimer}ms");
                    await Task.Delay(ReconnectTimer);
                }
            }
            while (true)
            {
                while (TaskQueue.IsEmpty)
                    Thread.Sleep(1);

                while (TaskQueue.TryDequeue(out var task))
                {
                    var putSomewhere = false;
                    while (!putSomewhere)
                    {
                        foreach (var connection in Connections)
                        {
                            if (connection.Used) continue;
                            
#pragma warning disable CS4014
                            connection.HandleTask(task);
#pragma warning restore CS4014
                            putSomewhere = true;
                            break;
                        }

                        if (!putSomewhere) Thread.Sleep(10);
                    }
                }
            }
        }
        catch(Exception ex)
        {
            if (ex is ThreadAbortException) return;

            Debugger.Print($"Fatal error on mysql thread!! '{ex.Message}'");
        }
    }

    
    private void InitConnections()
    {
        for(var i = 0; i < Connections.Length; i++)
        {
            Connections[i] = new Connection(this);
        }
    }
    public void Dispose()
    {
        LoopThread.Abort();
        foreach (var connection in Connections)
            connection.Close();
    }

    public async Task DisposeAsync()
    {
        LoopThread.Abort();
        foreach (var connection in Connections)
            await connection.CloseAsync();
    }

    /// <summary>
    /// Executes a mysql command
    /// </summary>
    /// <param name="command">The mysql command (cannot be null)</param>
    /// <param name="callback">Callback to handle the reader(keep null if just inserting/no query)</param>
    /// <param name="debugPrint">Print the command to the debugger</param>
    /// <returns>A awaitable task</returns>
    public async Task Execute(string command, Func<MySqlConnector.MySqlDataReader, Task> callback = null, bool debugPrint = false)
    {
        if (command == null) return;
        MySqlTask task = new() { Command = command, Finished = false, ReaderCallback = callback };
        TaskQueue.Enqueue(task);
        while (!task.Finished) await Task.Delay(1);
        if(debugPrint)
            Debugger.Print($"DEBUG SQL: {command}");
    }
}
