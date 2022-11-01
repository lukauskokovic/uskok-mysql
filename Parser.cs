using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Debugger = uskok_mysql.Debugger;

namespace MYSql;
public class MYSqlParser
{
    public MYSqlParser(Dictionary<Type, Func<object, object>> customWritings = null, Dictionary<Type, SQLCustomConversion> customReadings = null)
    {
        AddToDictiaonry(CustomWritings, customWritings);
        AddToDictiaonry(CustomReadings, customReadings);
    }

    private void AddToDictiaonry<Tkey, Tvalue>(Dictionary<Tkey, Tvalue> source, Dictionary<Tkey, Tvalue> addition)
    {
        if (addition == null) return;

        foreach(var item in addition)
        {
            source[item.Key] = item.Value;
        }
    }
    /// <summary>
    /// Dictionary with rules on how to serilize custom object types to primitive types (example TestClass->long)
    /// Example(pseudo code) CustomWritings[typeof(DateTime)] = (datetime) => (long)DateTime.ToUnix((DateTime)datetime)
    /// The example above converts datetime to long which will be stored in the table
    /// </summary>
    public Dictionary<Type, Func<object, object>> CustomWritings = new()
    {
        [typeof(DateTime)] = (obj) => 
        {
            DateTime time = (DateTime)obj;
            ulong unixTimestamp = (ulong)time.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            return unixTimestamp;
        }
    };
    /// <summary>
    /// Dictionary with rules on how to read and store primtive types in database and convert them to objects
    /// Example(pseudo code) (CustomReadings[typeof(DateTime)] = new SQLCustomConversion(typeof(long) <- unix timestamp, (unix) => DateTime.FromUnix(unix))
    /// The example above converts long to DateTime from unix timestamp
    /// </summary>
    public Dictionary<Type, SQLCustomConversion> CustomReadings = new()
    {
        [typeof(DateTime)] = new SQLCustomConversion(typeof(ulong), (obj) => 
        {
            if (obj is not long unix) return DateTime.MinValue;

            return (new DateTime(1970, 1, 1)).AddMilliseconds(unix);
        })
    };
    /// <summary>
    /// Dictionary of illegal characters in a string, (value is used to prevent sql injection and javascript injection)
    /// Default: (<, >, ')
    /// </summary>
    public HashSet<char> IllegalChars = new()
    {
        '<', '>', (char)39//(char)39 = '
    };
    /// <summary>
    /// Clears string of all illegal characters
    /// </summary>
    /// <param name="str">String to purfiy</param>
    /// <returns>Purified string</returns>
    public string PurifyString(string str)
    {
        StringBuilder builder = new();
        for (int charIndex = 0; charIndex < str.Length; charIndex++)
        {
            char c = str[charIndex];
            if (!IllegalChars.Contains(c))
                builder.Append(c);
        }
        return builder.ToString();
    }
    /// <summary>
    /// Parses/converts an object of known type to primitive type to be stored in the database
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="value">Value to be serilized </param>
    /// <returns>SPrimitive serilized type</returns>
    public object Parse<T>(T value) => Parse(value, typeof(T));
    /// <summary>
    /// Parses/converts the object to the value the will be serilized in the database
    /// DateTime example, Parse(DateTime.Now, typeof(DateTime) returns unix timestamp
    /// </summary>
    /// <param name="value">Value to be serilized</param>
    /// <param name="type">Type of the value</param>
    /// <returns>Primitive serilized type</returns>
    public object Parse(object value, Type type)
    {
        if (CustomWritings.TryGetValue(type, out var func)) return func(value);

        return value;
    }
    /// <summary>
    /// Get the sql string of an object
    /// </summary>
    /// <param name="obj">Any parser accepted object</param>
    /// <returns>String of the object(GetSQLString("test") => 'test', GetSQLString(DateTime.Now) => 12515215(unix now)))</returns>
    public string GetSQLString(object obj, Type type)
    {
        object parameter = Parse(obj, type);
        
        if (parameter == null)
            return "null";
        
        
        if (parameter is string str) return $"'{PurifyString(str)}'";
        else return parameter.ToString();
    }
    /// <summary>
    /// Reads data from the reader
    /// </summary>
    /// <param name="ordinal">Column index of the variable</param>
    /// <param name="type">Type of the variable(type in the object)</param>
    /// <returns>Returns read isntance of the object in the specified type</returns>
    public async Task<object> ReadType(MySqlConnector.MySqlDataReader reader, int ordinal, Type type)
    {
        if (await reader.IsDBNullAsync(ordinal)) return null;
        if (type == typeof(int)) return reader.GetInt32(ordinal);
        if (type == typeof(uint)) return reader.GetUInt32(ordinal);
        if (type == typeof(long)) return reader.GetInt64(ordinal);
        if (type == typeof(ulong)) return reader.GetUInt64(ordinal);
        if (type == typeof(string)) return reader.GetString(ordinal);
        if (type == typeof(bool)) return reader.GetBoolean(ordinal);
        return null;
    }
}
public class SQLCustomConversion
{
    public Type TypeInTable;
    public Func<object, object> Callback;
    /// <summary>
    /// Type of the value in CustomWrintings
    /// </summary>
    /// <param name="typeInTable">Type that is going to be stored in the table(DateTime -> typeof(long))</param>
    /// <param name="callback">Callback to the creating of the serilized object</param>
    public SQLCustomConversion(Type typeInTable, Func<object, object> callback)
    {
        TypeInTable = typeInTable;
        Callback = callback;
    }
}