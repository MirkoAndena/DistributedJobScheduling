using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Client to multicast, search for someone to submit a job
    /// </summary>
    [Serializable]
    public class WorkerAvailableRequest : Message
    {
        [JsonConstructor]
        public WorkerAvailableRequest() : base()
        {

        }
    }

    /// <summary>
    ///  Worker to Client, response
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class WorkerAvailableResponse : Message
    {
        public WorkerAvailableResponse(WorkerAvailableRequest request) : base(request)
        {

        }
    }
}