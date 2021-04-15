using System;
using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Storage.SecureStorage;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Client
{
    [Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class ClientJob
    {
        public int ID;
        public IJobResult Result;

        public ClientJob(int id)
        {
            ID = id;
        }
    }
}