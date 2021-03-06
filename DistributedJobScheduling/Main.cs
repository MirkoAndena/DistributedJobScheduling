using System;
using DistributedJobScheduling;
using DistributedJobScheduling.Vi;

public class Program
{
    static void Main(string[] args)
    {
        (int, bool)? commandlineParams = ParseCommandLineArgs(args);
        if (!commandlineParams.HasValue)
        {
            Console.WriteLine("ID not specified on launch");
            return;
        }

        Group group = new Group(commandlineParams.Value.Item1, commandlineParams.Value.Item2);
        
    }

    private static (int, bool)? ParseCommandLineArgs(string[] args)
    {
        int id;
        bool isId = Int32.TryParse(args.Length > 0 ? args[0] : "", out id);
        bool coordinator = args.Length > 1 && args[1].ToLower() == "coordinator";
        if (!isId) return null;
        return (id, coordinator);
    }
}