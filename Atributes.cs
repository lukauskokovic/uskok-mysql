using System;

namespace uskok_mysql.Attribues;
/// <summary>
/// Used for specyfing a primary key in a column
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class PrimaryKey : Attribute{}

/// <summary>
/// Used for specying a column that is to be incremented automaticlly(will be pased as null when inserting)
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class AutoIncrement : Attribute { }

/// <summary>
/// Setting the column to NOT NULL
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class NotNull : Attribute { }

/// <summary>
/// Used to mark a field that is to be ignored via the mysql parser
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ColumnIgnore : Attribute { }
/// <summary>
/// Used only for string type! Converts from mysql type TEXT->VARCHAR(length)
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class MaxLength : Attribute
{
    public int Length;
    public MaxLength(int length)
    {
        Length = length;
    }
}
/// <summary>
/// Used to specify a custom name for a column
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ColumnName : Attribute
{
    public string Name;
    public ColumnName(string name)
    {
        Name = name;
    }
}