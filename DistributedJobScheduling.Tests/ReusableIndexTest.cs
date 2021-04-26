using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DistributedJobScheduling.Storage;
using Xunit;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.Tests;
using Xunit.Abstractions;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.DistributedStorage
{
    public class ReusableIndexTest
    {
        private ReusableIndex _index;
        private BlockingDictionarySecureStore<Dictionary<int, bool>, int, bool> _items;
        private ITestOutputHelper _outputHelper;

        public ReusableIndexTest(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            var store = new MemoryStore<Dictionary<int, bool>>();
            var logger = new StubLogger(outputHelper);
            _items = new BlockingDictionarySecureStore<Dictionary<int, bool>, int, bool>(store, logger);
            _index = new ReusableIndex(_items);
            _items.Init();
        }

        [Theory]
        [InlineData(1000)]
        public void CheckOrder(int count)
        {
            for (int i = 0; i < count; i++)
                _items.Add(_index.NewIndex, true);
                
            for (int i = 0; i < _items.Keys.Count; i++)
            {
                Assert.Equal(i, _items.Keys.ElementAt(i));
                if (i > 0)
                    Assert.Equal(_items.Keys.ElementAt(i - 1) + 1, _items.Keys.ElementAt(i));
            }
        }
        
        [Theory]
        [InlineData(10)]
        [InlineData(10000)]
        public async void ParallelCreation(int count)
        {
            Task[] tasks = new Task[5];
            int countPerTask = count / tasks.Length;
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = Task.Run(() => 
                {
                    for (int j = 0; j < countPerTask; j++)
                    {
                        lock(_items)
                        {
                            _items.Add(_index.NewIndex, true);
                        }
                    }
                });

            await Task.WhenAll(tasks);

            for (int i = 0; i < count; i++)
                    Assert.True(_items.ContainsKey(i));
        }
    }
}