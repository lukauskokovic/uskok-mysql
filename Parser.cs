using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MYSql;
public class MYSqlParser
{
    public MYSqlParser(Dictionary<Type, Func<object, object>> customWritings = null, Dictionary<Type, SQLCustomConversion> customReadings = null, Dictionary<Type, string> customTypes = null)
    {
        CustomConversions = customWritings ?? CustomConversions;
        CustomReadings = customReadings ?? CustomReadings;
        CustomMYSQLTypes = customTypes ?? CustomMYSQLTypes;
    }
    public Dictionary<Type, Func<object, object>> CustomConversions = new()
    {
        [typeof(DateTime)] = (obj) => 
        {
            DateTime time = (DateTime)obj;
            long unixTimestamp = (long)time.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            return unixTimestamp;
        }
    };

    public Dictionary<Type, SQLCustomConversion> CustomReadings = new()
    {
        [typeof(DateTime)] = new SQLCustomConversion(typeof(long), (obj) => 
        {
            if (obj is not long unix) return DateTime.MinValue;

            return (new DateTime(1970, 1, 1)).AddMilliseconds(unix);
        })
    };
    public Dictionary<Type, string> CustomMYSQLTypes = new() 
    {
        [typeof(DateTime)] = "BIGINT UNSIGNED"
    };
    /// <summary>
    /// Dictionary of illegal characters in a string, (value is used 
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

    public object Parse<T>(T value) => Parse(value, typeof(T));

    public object Parse(object value, Type type)
    {
        if (CustomConversions.TryGetValue(type, out var func)) return func(value);

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

    public object ReadType(MySqlConnector.MySqlDataReader reader, int ordinal, Type type)
    {
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

    public SQLCustomConversion(Type typeInTable, Func<object, object> callback)
    {
        TypeInTable = typeInTable;
        Callback = callback;
    }
}