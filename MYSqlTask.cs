using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MYSql;
internal class MYSqlTask
{
    internal bool Finished = false;
    internal string Command = null;
    internal Func<MySqlConnector.MySqlDataReader, Task> ReaderCallback = null;
}

