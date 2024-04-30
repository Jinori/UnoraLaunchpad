using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace UnoraLaunchpad.Extensions;

public static class ParallelEx
{
    /// <summary>
    ///     ForEachAsync extension method for IEnumerable using TPL blocks
    /// </summary>
    /// <param name="source"></param>
    /// <param name="body"></param>
    /// <param name="maxDegreeOfParallelism"></param>
    /// <typeparam name="T"></typeparam>
    public static async Task ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> body, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded)
    {
        var block = new ActionBlock<T>(
            body,
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
            });

        foreach (var item in source)
            block.Post(item);
        
        block.Complete();

        await block.Completion;
    }
}