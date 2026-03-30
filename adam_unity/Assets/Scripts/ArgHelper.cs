using UnityEngine;

class ArgHelper
{
    public static string LogArgs()
    {
        var args = System.Environment.GetCommandLineArgs();

        string logString = "";
        foreach (var arg in args)
        {
            logString += arg + "\n";
        }
        
        return logString;
    }
    
    private static string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Split("=")[0] == name)
            {
                return args[i].Split("=")[1];
            }
        }
        return null;
    }

    private static int GetArgInt(string name, int defaultValue)
    {
        var arg = GetArg(name);

        if (arg == null)
            return defaultValue;
        else
            return int.Parse(arg);
    }
    
    private static string GetArgString(string name, string defaultValue)
    {
        var arg = GetArg(name);

        if (arg == null)
            return defaultValue;
        else
            return arg;
    }

    public static int PythonServerPort => GetArgInt("--agent-port",50000);
    public static int UnityServerPort => GetArgInt("--tool-port",60000);
    public static int TimeScale => GetArgInt("--timescale", -1);
    public static string PythonExePath => GetArgString("--python-exe", "INVALID_PYTHON-EXE_PATH");
    public static string PythonServerPath => GetArgString("--agent-app", "INVALID_PYTHON-SERVER_PATH");
    public static string CsvPath => GetArgString("--runs-path", "INVALID_CSV_PATH");
    public static string ExperimentName => GetArgString("--exp-name", null);
    public static int Parallelism
    {
        get
        {
            int aliasValue = GetArgInt("-p", 1);
            int paramValue = GetArgInt("--parallelism", 1);
            
            int returnValue = 0;
            if (aliasValue != 1)
                returnValue = aliasValue;
            else
                returnValue = paramValue;

            //Debug.Log($"Parallelism Set to {returnValue}");
            
            return returnValue;
        }
    }
}