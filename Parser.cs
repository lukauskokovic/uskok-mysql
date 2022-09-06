using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MYSql;
public class MYSqlParser
{
    public Dictionary<Type, Func<object, object>> CustomConversions = new();
    public Dictionary<Type, string> CustomMYSQLTypes = new();

    public object Parse<T>(T value) => Parse(value, typeof(T));

    public object Parse(object value, Type type)
    {
        if (CustomConversions.TryGetValue(type, out var func)) return func(value);

        return value;
    }
}