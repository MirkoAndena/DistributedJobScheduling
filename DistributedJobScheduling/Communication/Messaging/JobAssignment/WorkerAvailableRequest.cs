using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Client to multicast, search for someone to submit a job
    /// </summary>
    [Serializable]
    public class WorkerAvaiableRequest : Message
    {
        [JsonConstructor]
        public WorkerAvaiableRequest() : base()
        {

        }
    }

    /// <summary>
    ///  Worker to Client, response
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class WorkerAvaiableResponse : Message
    {
        public WorkerAvaiableResponse(WorkerAvaiableRequest request) : base(request)
        {

        }
    }
}