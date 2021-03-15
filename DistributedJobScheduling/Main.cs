using System;
using DistributedJobScheduling;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.DistributedStorage;
using DistributedJobScheduling.DistributedStorage.SecureStorage;
using DistributedJobScheduling.Logging;

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

        CreateInstances();
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

    private static void CreateInstances()
    {
        DependencyManager.Instance.RegisterSingletonServiceInstance<IStore, Storage>(new Storage());
        DependencyManager.Instance.RegisterSingletonServiceInstance<ILogger, CsvLogger>(new CsvLogger("../"));
    }
}