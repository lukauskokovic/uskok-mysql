using uskok_mysql.Attribues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace uskok_mysql;

public class DatabaseTable<T> where T : class, new()
{
    private readonly string _name;
    private readonly Column[] _columns;
    private readonly int _primaryKeyIndex = -1;
    private readonly Database _parent;
    public bool Created = false;
    public DatabaseTable(string tableName, Database parentDatabase)
    {
        _name = tableName;
        _parent = parentDatabase;
        List<Column> columnList = new();
        var fields = typeof(T).GetFields();
        StringBuilder initBuilder = new($"CREATE TABLE IF NOT EXISTS `{_name}` (");
        for (var i = 0; i < fields.Length; i++)
        {
            var isAutoIncrement = false;
            var field = fields[i];
            if (field.GetCustomAttribute<ColumnIgnore>() is not null) continue;

            StringBuilder extraBuilder = new();
            var type = field.FieldType;
            if (parentDatabase.Parser.CustomReadings.TryGetValue(field.FieldType, out var customConversion))
                type = customConversion.TypeInTable;

            var typeString = string.Empty;
            if (type == typeof(string))
            {
                if (field.GetCustomAttribute<MaxLength>() is {} length)
                    typeString = $"VARCHAR({length.Length})";
                else typeString = "TEXT";
            }
            else if (type == typeof(int)) typeString = "INT";
            else if (type == typeof(uint)) typeString = "INT UNSIGNED";
            else if (type == typeof(long)) typeString = "BIGINT";
            else if (type == typeof(ulong)) typeString = "BIGINT UNSIGNED";
            else if (type == typeof(bool)) typeString = "BOOLEAN";
            else if (type == typeof(float)) typeString = "FLOAT";
            else if (type == typeof(double)) typeString = "DOUBLE";
            

            if (field.GetCustomAttribute<PrimaryKey>() is not null)
            {
                extraBuilder.Append(" PRIMARY KEY");
                _primaryKeyIndex = i;
            }
            if (field.GetCustomAttribute<NotNull>() is not null) extraBuilder.Append(" NOT NULL");
            if (field.GetCustomAttribute<AutoIncrement>() is not null)
            {
                extraBuilder.Append(" AUTO_INCREMENT");
                isAutoIncrement = true;
            }
            //Format: Name TYPE(TEXT, BIGINT) [AUTO_INCREMENT, PRIMARY KEY, NOT NULL(<- extras)][,(if not the last field)]
            var NameString = field.Name;
            if (field.GetCustomAttribute<ColumnName>() is ColumnName nameAttribute) NameString = nameAttribute.Name;
            initBuilder.Append($"{NameString} {typeString}{extraBuilder},");
            extraBuilder.Clear();
            Column column = new(field, NameString, isAutoIncrement);
            columnList.Add(column);
        }
        initBuilder.Remove(initBuilder.Length - 1, 1);//Removes the extra comma
        initBuilder.Append(')');
        _columns = columnList.ToArray();
        columnList.Clear();
        if (_columns.Length == 0)
        {
            Debugger.Print($"WARNING: Table {_name} is emtpy");
            initBuilder.Clear();
            return;
        }
        var command = initBuilder.ToString();
        initBuilder.Clear();
        parentDatabase.Execute(command).GetAwaiter().GetResult();
        Created = true;
    }

    public string GetInsertString(T value)
    {
        StringBuilder stringBuilder = new("(");
        foreach (var column in _columns)
        {
            if (column.AutoIncrement || value == null)
            {
                stringBuilder.Append("null,");
                continue;
            }
            stringBuilder.Append($"{_parent.Parser.GetSQLString(column.FieldInfo.GetValue(value), column.FieldInfo.FieldType)},");
        }
        stringBuilder.Remove(stringBuilder.Length - 1, 1);//remove last comma
        stringBuilder.Append("),");
        return stringBuilder.ToString();
    }

    public string GetSqlStringForArray(T[] values, bool replace)
    {
        if (values.Length == 0) return string.Empty;
        StringBuilder commandBuilder = new($"{(replace ? "REPLACE" : "INSERT")} INTO `{_name}` VALUES ");
        foreach (var value in values)
            commandBuilder.Append(GetInsertString(value));

        commandBuilder.Remove(commandBuilder.Length - 1, 1);//remove last comma
        commandBuilder.Append(';');
        return commandBuilder.ToString();
    }
    /// <summary>
    /// Inserts an array into the table(INSERT INTO)
    /// </summary>
    /// <param name="values">Element array</param>
    /// <returns>Awaitable task</returns>
    public async Task Insert(params T[] values) => await _parent.Execute(GetSqlStringForArray(values, false));
    public async Task Insert(IEnumerable<T> values) => await _parent.Execute(GetSqlStringForArray(values.ToArray(), false));
    
    
    /// <summary>
    /// Replace and array into the table(REPLACE INTO)
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public async Task Replace(params T[] values) => await _parent.Execute(GetSqlStringForArray(values, true));
    public Task Replace(IEnumerable<T> values) => Replace(values.ToArray());

    
    /// <summary>
    /// Inserts into the table and reutrns the ids assosiated
    /// </summary>
    /// <param name="values">Array of values to insert</param>
    /// <returns>Array of ids</returns>
    /// <exception cref="Exception">: when the table does not contain a primary id with auto increment</exception>
    public async Task<object[]> InsertAndReturnAIIds(params T[] values)
    {
        if (values.Length == 0) return Array.Empty<object>();
        if (_primaryKeyIndex == -1 || !_columns[_primaryKeyIndex].AutoIncrement) throw new Exception($"'{_name}': table has no primary key with auto increment");
        var idColumn = _columns[_primaryKeyIndex];
        var command = $"{GetSqlStringForArray(values, false)}SELECT {idColumn.Name} from {_name} order by {idColumn.Name} desc limit {values.Length};";
        var ids = new object[values.Length];
        
        await _parent.Execute(command, async reader =>
        {
            for (var i = ids.Length-1; i >= 0 && await reader.ReadAsync(); i--)
            {
                ids[i] = await _parent.Parser.ReadType(reader, 0, idColumn.FieldInfo.FieldType);
            }
            
        }, true);
        return ids;
    }
    public Task<object[]> InsertAndReturnAIIds(IEnumerable<T> values) => InsertAndReturnAIIds(values.ToArray());

    /// <summary>
    /// Retruns all elements from the table
    /// </summary>
    /// <param name="whereCommand">NOT SQL INJECTION TESTED(USE PARESER.PURIFY FOR ALL INPUTS)!!!!Optional where query(Without: SELECT * FROM `table`, With: SELECT * FROM `table` WHERE `whereCommand`)</param>
    /// <param name="alias">Used along side where command, can be used to set the `table` as alias, example: All("pikac.id = 2", "pikac") so the query becomes
    /// <param name="selector">Replace the selector in sql request</param>
    /// SELECT * FROM `table` as 'pikac' WHERE pikac.id = 2</param>
    /// <param name="selector">SELECT {selector(default *)} FROM</param>
    /// <param name="debugPrint">Print the sql command</param>
    /// <returns>List of all the elements read (empty if none)</returns>
    public async Task<List<T>> All(string whereCommand = null, string alias = null, string selector = null, bool debugPrint = false)
    {
        List<T> list = new();
        StringBuilder commandBuilder = new($"SELECT {(selector ?? "*")} FROM `{_name}`");
        if (alias != null) commandBuilder.Append($" as {alias}");
        if (whereCommand != null) commandBuilder.Append($" WHERE {whereCommand}");
        var command = commandBuilder.ToString();
        commandBuilder.Clear();
        if (debugPrint)
        {
            Debugger.Print($"DEBUG SQL: {command}");
        }
        await _parent.Execute(command, async (reader) => 
        {
            while (await reader.ReadAsync())
            {
                T instance = new();
                for(var i = 0; i < _columns.Length; i++)
                {
                    var column = _columns[i];
                    
                    var isCustom = _parent.Parser.CustomReadings.TryGetValue(column.FieldInfo.FieldType, out var customConversion);
                    var typeToRead = isCustom? customConversion.TypeInTable : column.FieldInfo.FieldType;

                    var readValue = await _parent.Parser.ReadType(reader, i, typeToRead);

                    if (isCustom) readValue = customConversion.Callback(readValue);

                    column.FieldInfo.SetValue(instance, readValue);
                }
                list.Add(instance);
            }
        });
        return list;
    }

    public async Task<List<T>> GetByID<TIdType>(TIdType id)
    {
        if (_primaryKeyIndex == -1) throw new Exception($"Table {_name} has no primary key");
        return await All($"{_columns[_primaryKeyIndex].Name} = {_parent.Parser.GetSQLString(id, typeof(TIdType))}");
    }
    
    public async Task<T> GetByIDSingle<TIdType>(TIdType id)
    {
        if (_primaryKeyIndex == -1) throw new Exception($"Table {_name} has no primary key");
        var list = await All($"{_columns[_primaryKeyIndex].Name} = {_parent.Parser.GetSQLString(id, typeof(TIdType))} LIMIT 1");
        return list.Count <= 0 ? null : list[0];
    }

    public Task<List<T>> GetByColumn<TIdType>(string columnName, IEnumerable<TIdType> ids, string where = null, bool debugPrint = false) =>
        GetByColumn(where, debugPrint, columnName, ids.ToArray());
    public async Task<List<T>> GetByColumn<TIdType>(string where, bool debugPrint, string columnName, params TIdType[] ids)
    {
        var list = new List<T>();
        if (columnName == null || ids.Length == 0) return list;
        var builder = new StringBuilder(where == null? string.Empty : $"{where} AND (");
        for (var i = 0; i < ids.Length; i++)
        {
            var isLast = i + 1 >= ids.Length;
            builder.Append(
                $"{columnName}={_parent.Parser.GetSQLString(ids[i], typeof(TIdType))}{(isLast ? string.Empty : " or ")}");
        }

        if (where != null)
            builder.Append(')');
        return await All(builder.ToString(), debugPrint: debugPrint);
    }

    public Task<List<T>> GetByIds<TIdType>(IEnumerable<TIdType> ids) => GetByIds(ids.ToArray());
    public async Task<List<T>> GetByIds<TIdType>(params TIdType[] ids)
    {
        if (_primaryKeyIndex == -1) throw new Exception($"Table {_name} has no primary key");
        if (ids.Length == 0) return new List<T>();
        return await GetByColumn(_columns[_primaryKeyIndex].Name, ids);
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