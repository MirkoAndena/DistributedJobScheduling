using System.Collections.Generic;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.Storage;
using System.Threading.Tasks;
using System.Threading;

namespace DistributedJobScheduling.JobAssignment
{
    public class JobUtils
    { 
        private static Dictionary<int, int> FindNodesOccurrences(Group group, ILogger logger, BlockingDictionarySecureStore<Dictionary<int, Job>, int, Job> secureStore)
        {
            // Init each node with no occurrences
            Dictionary<int, int> nodeJobCount = new Dictionary<int, int>();
            nodeJobCount.Add(group.Me.ID.Value, 0);
            if (!group.ImCoordinator) nodeJobCount.Add(group.Coordinator.ID.Value, 0);
            group.Others.ForEach(node => nodeJobCount.Add(node.ID.Value, 0));

            // For each node calculate how many jobs are assigned
            secureStore.Values.ForEach(job =>
            {
                // Here should be always true
                if (job.Node.HasValue)
                {
                    if (nodeJobCount.ContainsKey(job.Node.Value))
                        nodeJobCount[job.Node.Value]++;
                    else
                        nodeJobCount.Add(job.Node.Value, 1);
                }
            });

            logger.Log(Tag.JobUtils, $"Total job assigned per node: {nodeJobCount.ToString<int, int>()}");
            return nodeJobCount;
        }

        public static int FindNodeWithLessJobs(Group group, ILogger logger, BlockingDictionarySecureStore<Dictionary<int, Job>, int, Job> secureStore)
        {
            Dictionary<int, int> nodeJobCount = FindNodesOccurrences(group, logger, secureStore);

            // Find the node with the less number of assignment
            (int, int) min = (group.Me.ID.Value, nodeJobCount[group.Me.ID.Value]);
            nodeJobCount.ForEach(nodeOccurencesPair => 
            {
                if (nodeOccurencesPair.Value < min.Item2)
                    min = (nodeOccurencesPair.Key, nodeOccurencesPair.Value);
            });

            logger.Log(Tag.JobUtils, $"Node with less occurrences: {min.Item1} with {min.Item2} jobs to do");
            return min.Item1;
        }
    }
}