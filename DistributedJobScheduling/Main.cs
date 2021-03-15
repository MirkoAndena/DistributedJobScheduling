using System;
using DistributedJobScheduling.LifeCycle;

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
        Console.WriteLine($"Params: ID = {commandlineParams.Value.Item1}, Coordinator = {commandlineParams.Value.Item2}");

        SystemLifeCycle.Run();
        //var nodeRegistry = DependencyManager.Get<Node.INodeRegistry>();
        //Node me = nodeRegistry.GetOrCreate(id: commandlineParams.Value.Item1);
        //Group group = new Group(me, commandlineParams.Value.Item2);
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