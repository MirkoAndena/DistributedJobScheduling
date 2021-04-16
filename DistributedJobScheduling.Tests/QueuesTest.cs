using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Queues;
using Xunit;

namespace DistributedJobScheduling.DistributedStorage
{
    public class QueuesTest
    {

        [Fact]
        public async Task AsyncQueueTest()
        {
            AsyncGenericQueue<int> queue = new AsyncGenericQueue<int>();
            int numbersToProduce = 1000000;
            int nProducers = 100;
            int production = numbersToProduce / nProducers;

            Task<List<int>>[] workers = new Task<List<int>>[nProducers+1];
            for(int i = 0; i < workers.Length - 1; i++)
                workers[i] = ProduceElement(queue, production, (int)Math.Pow(2, i%7));
            workers[workers.Length - 1] = ConsumeQueue(queue, numbersToProduce);

            Task timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(4000));
            Task produceConsume = Task.WhenAll(workers);
            await Task.WhenAny(produceConsume,
                                timeoutTask);

            Assert.False(timeoutTask.IsCompleted);

            List<int> producedSet = new List<int>();
            for(int i = 0; i < workers.Length - 1; i++)
                producedSet.AddRange(workers[i].Result);
            producedSet.Sort();

            var consumedSet = workers[workers.Length - 1].Result;
            consumedSet.Sort();
            
            Assert.True(producedSet.Count == consumedSet.Count);
            for(int i = 0; i < numbersToProduce; i++)
                Assert.True(producedSet[i] == consumedSet[i], $"Element {i} is different");
        }

        private async Task<List<int>> ProduceElement(AsyncGenericQueue<int> queue, int production, int batchSize)
        {
            Random rnd = new Random();
            List<int> producedNumbers = new List<int>(production);
            int passes = (int)Math.Ceiling((double)production/batchSize) * batchSize;
            int[] currentProduction = new int[batchSize];
            for(int i = 0; i < passes; i+=batchSize)
            {
                int producedCount = 0;
                for(int j = 0; j < batchSize; j++)
                {
                    if(producedNumbers.Count < production)
                    {
                        int produced = rnd.Next();
                        currentProduction[j] = produced;
                        producedNumbers.Add(produced);
                        producedCount = j;
                    }
                }
                producedCount += 1;

                await Task.Yield();
                if(batchSize == 1)
                    queue.Enqueue(currentProduction[0]);
                else if(producedCount > 0)
                    queue.EnqueueRange(new ArraySegment<int>(currentProduction, 0, producedCount));
            }
            return producedNumbers;
        }

        private async Task<List<int>> ConsumeQueue(AsyncGenericQueue<int> queue, int expectedSize)
        {
            List<int> results = new List<int>(expectedSize);
            while(results.Count < expectedSize)
            {
                int number = await queue.Dequeue();
                results.Add(number);
            }
            return results;
        }
    }
}