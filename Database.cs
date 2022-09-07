using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MYSql;
public class Database : IDisposable
{
    internal readonly string ConnectionString;
    public readonly MYSqlParser Parser;
    private readonly Connection[] Connections;
    private readonly Thread LoopThread;
    private readonly ConcurrentQueue<MYSqlTask> TaskQueue = new();
    /// <summary>
    /// Creates a database instance
    /// </summary>
    /// <param name="connectionString">Connection string for mysql database</param>
    /// <param name="connections">Connection pool length(5 is default)</param>
    /// <param name="customConversion">Custom conversion for specific types</param>
    public Database(string connectionString, int connections = 5, Dictionary<Type, Func<object, object>> customConversion = null, Dictionary<Type, string> customMYSqlTypes = null)
    {
        ConnectionString = connectionString;
        Parser = new() {
            CustomConversions = customConversion ?? new(),
            CustomMYSQLTypes = customMYSqlTypes ?? new()
        };
        Connections = new Connection[connections];
        LoopThread = new Thread(ThreadMethod);
        LoopThread.Start();
    }

    void ThreadMethod()
    {
        try
        {
            InitConnections();
            while (true)
            {
                while (TaskQueue.IsEmpty)
                {
                    Thread.Sleep(500);
                }

                while (TaskQueue.TryDequeue(out var task))
                {
                    bool _putSomewhere = false;
                    while (!_putSomewhere)
                    {
                        for (int i = 0; i < Connections.Length; i++)
                        {
                            if (!Connections[i].Used)
                            {
                                Connections[i].HandleTask(task);
                                _putSomewhere = true;
                                break;
                            }
                        }

                        if (!_putSomewhere) Thread.Sleep(10);
                    }
                }
            }
        }
        catch(Exception ex)
        {
            if (ex is ThreadAbortException) return;

            Console.WriteLine("Fatal error on mysql thread!!{0}", ex.Message);
        }
    }

    void InitConnections()
    {
        try
        {
            for(int i = 0; i < Connections.Length; i++)
            {
                Connections[i] = new(this);
            }
            Console.WriteLine("MYSql connections opened");
        }
        catch(Exception ex)
        {
            if (ex is ThreadAbortException) return;
            Console.WriteLine("Could not initilize mysql connections {0}", ex.Message);
        }
    }
    public void Dispose()
    {
        LoopThread.Abort();
        for(int i = 0; i < Connections.Length; i++)
            Connections[i].Close();
    }
    /// <summary>
    /// Executes a mysql command
    /// </summary>
    /// <param name="command">The mysql commmand (cannot be null)</param>
    /// <param name="callback">Callback to handle the reader(keep null if just inserting/no query)</param>
    /// <returns>A awaitable task</returns>
    public async Task Execute(string command!!, Func<MySqlConnector.MySqlDataReader, Task> callback = null)
    {
        MYSqlTask task = new() { Command = command, Finished = false, ReaderCallback = callback };
        TaskQueue.Enqueue(task);
        while (!task.Finished) await Task.Delay(10);
    }
}
