using System.Collections.Generic;

namespace DistributedJobScheduling.DistributedStorage
{
    public class DistributedStorage
    {
        private List<Job> _localStorage;

        public void DeletePendingJobs()
        {
            _localStorage.RemoveAll(job => job.Status == JobStatus.PENDING);
        }

        public void Assign(Job job)
        {
            foreach (Job stored in _localStorage)
            {
                if (stored == job)
                {
                    stored.Node = FindProperNode(job);
                    return;
                }
            }

            throw new System.Exception("No job found int the local storage");
        }

        private int FindProperNode(Job job)
        {
            // TODO trovare il nodo che ha il minor numero di job assegnati (che dovrebbe essere 1)
            return 0;
        }
    }
}