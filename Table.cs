using MYSql.Attribues;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MYSql;

public class DatabaseTable<T> where T : class
{
    private readonly string Name;
    private readonly Column[] Columns;
    private readonly Database Parent;
    public DatabaseTable(string tableName, Database parentDatabase)
    {
        Name = tableName;
        Parent = parentDatabase;
        List<Column> ColumnList = new();
        FieldInfo[] Fields = typeof(T).GetFields();
        StringBuilder InitBuilder = new($"CREATE TABLE IF NOT EXISTS `{Name}` (");
        for(int i = 0; i < Fields.Length; i++)
        {
            FieldInfo field = Fields[i];
            if (field.GetCustomAttribute<ColumnIgnore>() is not null) continue;
            Column column = new(field);
            string TypeString = string.Empty;
            StringBuilder ExtraBuilder = new();

            if(!parentDatabase.Parser.CustomMYSQLTypes.TryGetValue(field.FieldType, out TypeString))
            {
                if (field.FieldType == typeof(string))
                {
                    if (field.GetCustomAttribute<MaxLength>() is MaxLength length)
                        TypeString = $"VARCHAR({length.Length})";
                    else TypeString = "TEXT";
                }
                else if (field.FieldType == typeof(int)) TypeString = "INT";
                else if (field.FieldType == typeof(uint)) TypeString = "UNSIGNED INT";
                else if (field.FieldType == typeof(long)) TypeString = "BIGINT";
                else if (field.FieldType == typeof(ulong)) TypeString = "UNSIGNED BIGINT";
            }

            if (field.GetCustomAttribute<PrimaryKey>() is not null) ExtraBuilder.Append(" PRIMARY KEY");
            if (field.GetCustomAttribute<NotNull>() is not null) ExtraBuilder.Append(" NOT NULL");
            if (field.GetCustomAttribute<AutoIncrement>() is not null) ExtraBuilder.Append(" AUTO_INCREMENT");
            //Format: Name TYPE(TEXT, BIGINT) [AUTO_INCREMENT, PRIMARY KEY, NOT NULL(<- extras)][,(if not the last field)]
            InitBuilder.Append($"{field.Name} {TypeString}{ExtraBuilder},");
            ExtraBuilder.Clear();
            ColumnList.Add(column);
        }
        InitBuilder.Remove(InitBuilder.Length-1, 1);//Removes the extra comma
        InitBuilder.Append(')');
        Columns = ColumnList.ToArray();
        ColumnList.Clear();
        if (Columns.Length == 0)
        {
            Console.WriteLine($"WARNING: Table {Name} is emtpy");
            InitBuilder.Clear();
            return; 
        }
        string Command = InitBuilder.ToString();
        InitBuilder.Clear();
        _ = parentDatabase.Execute(Command);
    }
}

internal class Column
{
    internal readonly FieldInfo FieldInfo;

    internal Column(FieldInfo fieldInfo)
    {
        FieldInfo = fieldInfo;
    }
}