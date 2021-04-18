using System.Linq;
using System.Threading;
using System.Collections.Generic;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.Tests.Utils;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit.Abstractions;
using Xunit;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Storage;

namespace DistributedJobScheduling.Tests.Jobs
{
    using Store = BlockingDictionarySecureStore<Dictionary<int, Job>, int, Job>;

    public class JobTestUtils
    {
        public static void StoreJobs(ReusableIndex index, Group group, Store secureStore)
        {
            // Me => 4
            // Coordinator => 3
            // Others => ID number

            StoreJob(index, secureStore, group.Me);
            StoreJob(index, secureStore, group.Me);
            StoreJob(index, secureStore, group.Me);
            StoreJob(index, secureStore, group.Me);
            
            StoreJob(index, secureStore, group.Coordinator);
            StoreJob(index, secureStore, group.Coordinator);
            StoreJob(index, secureStore, group.Coordinator);

            group.Others.ForEach(node => Enumerable.Range(0, node.ID.Value).ForEach(i => StoreJob(index, secureStore, node)));
        }

        public static void StoreJob(ReusableIndex index, Store secureStore, Node owner)
        {
            Job job = CreateJob(index, owner);
            secureStore.Add(job.ID.Value, job);
        }

        public static Job CreateJob(ReusableIndex index, Node owner)
        {
            Job job = new TimeoutJob(1);
            job.ID = index.NewIndex;
            job.Node = owner.ID.Value;
            return job;
        }
    }
}