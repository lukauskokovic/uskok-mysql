using MYSql.Attribues;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MYSql;

public class DatabaseTable<T> where T : class
{
    private readonly string Name;
    private readonly Column[] Columns;
    private readonly int PrimaryKeyIndex = -1;
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
            bool isAutoIncrement = false;
            FieldInfo field = Fields[i];
            if (field.GetCustomAttribute<ColumnIgnore>() is not null) continue;
            
            StringBuilder ExtraBuilder = new();

            if(!parentDatabase.Parser.CustomMYSQLTypes.TryGetValue(field.FieldType, out string TypeString))
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

    private string GetInsertString(T value, bool replace)
    {
        StringBuilder stringBuilder = new($"{(replace? "REPLACE" : "INSERT")} INTO `{Name}` VALUES (");
        for(int i = 0; i < Columns.Length; i++)
        {
            if (Columns[i].AutoIncrement)
            {
                stringBuilder.Append("null,");
                continue;
            }
            object parameter = Parent.Parser.Parse(Columns[i].FieldInfo.GetValue(value), Columns[i].FieldInfo.FieldType);
            if(parameter == null)
            {
                stringBuilder.Append("null,");
                continue;
            }


            if (parameter is string str)
            {
                stringBuilder.Append("'");
                for (int charIndex = 0; charIndex < str.Length; charIndex++)
                {
                    char c = str[charIndex];
                    if (!Parent.Parser.IllegalChars.Contains(c))
                        stringBuilder.Append(c);
                }
                stringBuilder.Append("',");
            }
            else stringBuilder.Append($"{parameter},");
        }
        stringBuilder.Remove(stringBuilder.Length - 1, 1);//remove last comma
        stringBuilder.Append(");");
        return stringBuilder.ToString();
    }

    private string GetSQLStringForArray(T[] values, bool replace)
    {
        if (values.Length == 0) return string.Empty;

        StringBuilder CommandBuilder = new();
        for (int i = 0; i < values.Length; i++)
            CommandBuilder.Append(GetInsertString(values[i], replace));

        return CommandBuilder.ToString();
    }

    public async Task Insert(params T[] values) => await Parent.Execute(GetSQLStringForArray(values, false));
    public async Task Replace(params T[] values) => await Parent.Execute(GetSQLStringForArray(values, true));


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