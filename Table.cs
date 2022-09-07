using MYSql.Attribues;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MYSql;

public class DatabaseTable<T> where T : class, new()
{
    private readonly string Name;
    private readonly Column[] Columns;
    private readonly int PrimaryKeyIndex = -1;
    private readonly Database Parent;
    public bool Created = false;
    public DatabaseTable(string tableName, Database parentDatabase)
    {
        Name = tableName;
        Parent = parentDatabase;
        List<Column> ColumnList = new();
        FieldInfo[] Fields = typeof(T).GetFields();
        StringBuilder InitBuilder = new($"CREATE TABLE IF NOT EXISTS `{Name}` (");
        for (int i = 0; i < Fields.Length; i++)
        {
            bool isAutoIncrement = false;
            FieldInfo field = Fields[i];
            if (field.GetCustomAttribute<ColumnIgnore>() is not null) continue;

            StringBuilder ExtraBuilder = new();

            if (!parentDatabase.Parser.CustomMYSQLTypes.TryGetValue(field.FieldType, out string TypeString))
            {
                if (field.FieldType == typeof(string))
                {
                    if (field.GetCustomAttribute<MaxLength>() is MaxLength length)
                        TypeString = $"VARCHAR({length.Length})";
                    else TypeString = "TEXT";
                }
                else if (field.FieldType == typeof(int)) TypeString = "INT";
                else if (field.FieldType == typeof(uint)) TypeString = "INT UNSIGNED";
                else if (field.FieldType == typeof(long)) TypeString = "BIGINT";
                else if (field.FieldType == typeof(ulong)) TypeString = "BIGINT UNSIGNED";
                else if (field.FieldType == typeof(bool)) TypeString = "BOOLEAN";
            }

            if (field.GetCustomAttribute<PrimaryKey>() is not null)
            {
                ExtraBuilder.Append(" PRIMARY KEY");
                PrimaryKeyIndex = i;
            }
            if (field.GetCustomAttribute<NotNull>() is not null) ExtraBuilder.Append(" NOT NULL");
            if (field.GetCustomAttribute<AutoIncrement>() is not null)
            {
                ExtraBuilder.Append(" AUTO_INCREMENT");
                isAutoIncrement = true;
            }
            //Format: Name TYPE(TEXT, BIGINT) [AUTO_INCREMENT, PRIMARY KEY, NOT NULL(<- extras)][,(if not the last field)]
            string NameString = field.Name;
            if (field.GetCustomAttribute<ColumnName>() is ColumnName nameAttribute) NameString = nameAttribute.Name;
            InitBuilder.Append($"{NameString} {TypeString}{ExtraBuilder},");
            ExtraBuilder.Clear();
            Column column = new(field, NameString, isAutoIncrement);
            ColumnList.Add(column);
        }
        InitBuilder.Remove(InitBuilder.Length - 1, 1);//Removes the extra comma
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
        Task.Run(async () => 
        {
            await parentDatabase.Execute(Command);
            Created = true;
        });
    }

    private string GetInsertString(T value)
    {
        StringBuilder stringBuilder = new("(");
        for (int i = 0; i < Columns.Length; i++)
        {
            var column = Columns[i];
            if (column.AutoIncrement)
            {
                stringBuilder.Append("null,");
                continue;
            }
            stringBuilder.Append($"{Parent.Parser.GetSQLString(column.FieldInfo.GetValue(value), column.FieldInfo.FieldType)},");
        }
        stringBuilder.Remove(stringBuilder.Length - 1, 1);//remove last comma
        stringBuilder.Append("),");
        return stringBuilder.ToString();
    }

    private string GetSQLStringForArray(T[] values, bool replace)
    {
        if (values.Length == 0) return string.Empty;

        StringBuilder CommandBuilder = new($"{(replace ? "REPLACE" : "INSERT")} INTO `{Name}` VALUES ");
        for (int i = 0; i < values.Length; i++)
            CommandBuilder.Append(GetInsertString(values[i]));

        CommandBuilder.Remove(CommandBuilder.Length - 1, 1);//remove last comma
        CommandBuilder.Append(';');
        return CommandBuilder.ToString();
    }
    /// <summary>
    /// Inserts an array into the table(INSERT INTO)
    /// </summary>
    /// <param name="values">Element array</param>
    /// <returns>Awaitable task</returns>
    public async Task Insert(params T[] values) 
    {
        Console.WriteLine("Getting string");
        var s = GetSQLStringForArray(values, false);
        Console.WriteLine("Executig");
        await Parent.Execute(s);
    }
    /// <summary>
    /// Replace and array into the table(REPLACE INTO)
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public async Task Replace(params T[] values) => await Parent.Execute(GetSQLStringForArray(values, true));


    /// <summary>
    /// Retruns all elements from the table
    /// </summary>
    /// <param name="whereCommand">NOT SQL INJECTION TESTED(USE PARESER.PURIFY FOR ALL INPUTS)!!!!Optional where query(Without: SELECT * FROM `table`, With: SELECT * FROM `table` WHERE `whereCommand`)</param>
    /// <param name="alias">Used along side where command, can be used to set the `table` as alias, example: All("pikac.id = 2", "pikac") so the query becomes
    /// SELECT * FROM `table` as 'pikac' WHERE pikac.id = 2</param>
    /// <returns>List of all the elements read (empty if none)</returns>
    public async Task<List<T>> All(string whereCommand = null, string alias = null)
    {
        List<T> List = new();
        StringBuilder CommandBuilder = new($"SELECT * FROM `{Name}`");
        if (alias != null) CommandBuilder.Append($" as {alias}");
        if (whereCommand != null) CommandBuilder.Append($" WHERE {whereCommand}");
        await Parent.Execute(CommandBuilder.ToString(), async (reader) => 
        {
            while (await reader.ReadAsync())
            {
                T instance = new();
                for(int i = 0; i < Columns.Length; i++)
                {
                    var column = Columns[i];
                    
                    bool isCustom = Parent.Parser.CustomReadings.TryGetValue(column.FieldInfo.FieldType, out var customConversion);
                    Type typeToRead = isCustom? customConversion.TypeInTable : column.FieldInfo.FieldType;

                    object readValue = Parent.Parser.ReadType(reader, i, typeToRead);

                    if (isCustom) readValue = customConversion.Callback(readValue);

                    column.FieldInfo.SetValue(instance, readValue);
                }
                List.Add(instance);
            }
        });
        CommandBuilder.Clear();
        return List;
    }

    public async Task<List<T>> GetByID<IDType>(IDType id)
    {
        if (PrimaryKeyIndex == -1) throw new Exception($"Table {Name} has no primary key");
        return await All($"{Columns[PrimaryKeyIndex].Name} = {Parent.Parser.GetSQLString(id, typeof(IDType))}");
    }

}

internal class Column
{
    internal readonly FieldInfo FieldInfo;
    internal readonly string Name;
    internal readonly bool AutoIncrement = false;

    internal Column(FieldInfo fieldInfo, string name, bool autoIncrement)
    {
        FieldInfo = fieldInfo;
        Name = name;
        AutoIncrement = autoIncrement;
    }
}