namespace MYSql;

public class DatabaseTable<T> where T : class
{
    private readonly string Name;
    public DatabaseTable(string tableName)
    {
        Name = tableName;

    }
}