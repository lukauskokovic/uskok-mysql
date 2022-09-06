using MYSql;
using MYSql.Attribues;
using System;
using System.Threading;
using System.Threading.Tasks;

class MainClass
{
    public static void Main()
    {
        Task.Run(async () => 
        {
            Database dBase = new("Server=localhost;Database=test;Uid=root;Pwd=;", 3);
            await dBase.Execute("INSERT INTO testtable VALUES (null, '3')");
            await dBase.Execute("INSERT INTO testtable VALUES (null, '3')");
            await dBase.Execute("INSERT INTO testtable VALUES (null, '3')");
            await dBase.Execute("INSERT INTO testtable VALUES (null, '3')");
            await dBase.Execute("INSERT INTO testtable VALUES (null, '3')");
            await dBase.Execute("INSERT INTO testtable VALUES (null, '3')");
            await dBase.Execute("INSERT INTO testtable VALUES (null, '3')");
            await dBase.Execute("INSERT INTO testtable VALUES (null, '3')");
            await dBase.Execute("SELECT * FROM testtable", async (reader) => 
            {
                Console.WriteLine("Reading {0}", await reader.ReadAsync());

            });
            Console.WriteLine("Finished");
        });
        Console.ReadKey(true);
    }
    static void Test()
    {
        Console.WriteLine("Starting task");
        Thread.Sleep(1000);
        Console.WriteLine("Tasl end");
    }

    static async Task DelayTask() => await Task.Delay(100);
}

class TestTable
{
    [PrimaryKey]
    [AutoIncrement]
    public int ID;
    [NotNull]
    [MaxLength(20)]
    public string Name;

    [ColumnIgnore]
    public bool Test;
}