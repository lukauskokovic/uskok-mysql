# Uskok-MYSQL
Uskok-MYSQL is a library that I used to do some private work so I thought why not upload it to github.
It uses .NET Reflections to achieve a bit painless mysql experinece in .NET.
Also it creates a multi threaded and async mysql connection pool so that creating a new connection for each request it not reuqired.
Amount of open connection at one time is customizable for each database.
# Examples

## Connecting to a database

```cs
//This line connects to a database in another thread and opens `x` connections
Database Database = new("ConnectionString", x);
```

## Creating a table

```cs
Database Database = new("ConnectionString", x);
class TestTableColumn{
  public int ID;
  public string Name;
}
//This creates a table and executes a mysql request to create the table if it does not exist.
//The create request 'CREATE TABLE IF NOT EXISTS `Table_Name`(ID INT, Name TEXT);'
DatabaseTable<TestTableColumn> Table = new("Table_Name", Database);
//You can also wait until it is assured the table exists
while(!Table.Created)await Task.Delay(1);
```

## Column Attributes

###### PrimaryKey
Used for specifing that the column is a primary key.
Example:
```cs
class Person{
  [PrimaryKey]
  public int SocialNumber;
  public string Name;
}
//Dont forget to connect to the database!!!
DatabaseTable<Person> Table = new("Table_Name", Database);
//The table creation requiest:
```
```sql
CREATE TABLE IF NOT EXIST `Table_Name` (SocialNumber INT PRIMARY KEY, Name TEXT);'
```

###### AutoIncrement
Used for marking a auto increment column
When inserting this column will autmaticlly be assigned to null
```cs
class Person{
  [AutoIncrement]
  public int SocialNumber;
  public string Name;
}
//Dont forget to connect to the database!!!
DatabaseTable<Person> Table = new("Table_Name", Database);
//The table creation requiest:
```
```sql
CREATE TABLE IF NOT EXIST `Table_Name` (SocialNumber INT AUTO_INCREMENT, Name TEXT);'
```

###### NotNull
Used for marking a column that is not null
```cs
class Person{
  public int SocialNumber;
  [NotNull]
  public string Name;
}
//Dont forget to connect to the database!!!
DatabaseTable<Person> Table = new("Table_Name", Database);
//The table creation requiest:
```
```sql
CREATE TABLE IF NOT EXIST `Table_Name` (SocialNumber INT, Name TEXT NotNull);'
```

###### ColumnIgnore
Used for marking a column that is to be ignored
```cs
class Person{
  public int SocialNumber;
  [ColumnIgnore]
  public string Name;
}
//Dont forget to connect to the database!!!
DatabaseTable<Person> Table = new("Table_Name", Database);
//The table creation requiest:
```
```sql
CREATE TABLE IF NOT EXIST `Table_Name` (SocialNumber INT);'
```

###### MaxLength
Used for specifying the max length in a string(converts it to varchar)
```cs
class Person{
  public int SocialNumber;
  [MaxLength(30)]
  public string Name;
}
//Dont forget to connect to the database!!!
DatabaseTable<Person> Table = new("Table_Name", Database);
//The table creation requiest:
```
```sql
CREATE TABLE IF NOT EXIST `Table_Name` (SocialNumber INT, Name VARCHAR(30));'
```

###### ColumnName
Used for setting a custom name to a column
```cs
class Person{
  public int SocialNumber;
  [ColumnName("namelowercase")]
  public string Name;
}
//Dont forget to connect to the database!!!
DatabaseTable<Person> Table = new("Table_Name", Database);
//The table creation requiest:
```
```sql
CREATE TABLE IF NOT EXIST `Table_Name` (SocialNumber INT, namelowercase TEXT);'
```
