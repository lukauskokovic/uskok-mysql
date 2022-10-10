using System;

namespace uskok_mysql;

public static class Debugger
{
    public static Action<object> OutputProvider = Console.WriteLine;
    public static void Print(object text) { OutputProvider?.Invoke($"USKOK_MYSQL: {text.ToString()}"); }
}