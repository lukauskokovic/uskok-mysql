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
            DatabaseTable<TestTable> test = new("test", dBase);
            TestTable[] tables = new TestTable[1000];
            for (int i = 0; i < tables.Length; i++)
                tables[i] = new()
                {
                    ID = i,
                    Name = $"kitac pitac {i}",
                    Test = i % 2 == 0
                };
            await test.Replace(tables);
            Console.WriteLine("Done");
        });
        Console.ReadKey(true);
    }
}

class TestTable
{
    [PrimaryKey]
    [AutoIncrement]
    public int ID;
    [NotNull]
    [MaxLength(20)]
    [ColumnName("kitac")]
    public string Name;
    public bool Test;
}