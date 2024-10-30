using System.Collections.Concurrent;
using System.Globalization;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;

namespace StreamTest;

public class ComputeActor: ReceiveActor
{
    private const int ParallelismFactor = 5;
    
    private readonly ILoggingAdapter _logger;
    
    public ComputeActor()
    {
        _logger = Context.GetLogger();

        Receive<Compute>(c => HandleCompute(c.BatchJobs, c.JobId));
    }
    
    private void HandleCompute(List<BatchJobInfo> batchJobs, int jobId)
    {
        var materializer = Context.System.Materializer();
        var sender = Sender;
        var hasError = false;
        var totalErrors = 0;
        var totalJobs = batchJobs.Count;
        var completedJobs = 0;
        
        var sink = Flow
                .Create<BatchJobInfo>()
                .Grouped(5)
                .Select(batchJob => new ComputationTask(
                    jobId,
                    batchJob.ToList()
                ))
                .SelectAsync(ParallelismFactor, async computationTask =>
                {
                    var source = await CreateStream(computationTask);
                    return new ComputationStream(jobId, batchJobs, source);
                })
                .SelectAsync(ParallelismFactor, async computationStream =>
                {
                    var computationStreamResult = await WriteStream(computationStream, materializer);
                    return computationStreamResult;
                })
                .AlsoTo(Sink.OnComplete<ComputationStreamResult>(() =>
                {
                    _logger.Info("Completed computation task stream. jobId: {0}, totalErrors: {1}", jobId, totalErrors);
                    sender.Tell(new UpdateItem<int, Job<string>>(jobId,
                        new Job<string>(jobId, hasError || totalErrors > 0 ? JobStatus.Error : JobStatus.Complete,
                            jobId.ToString(), DateTime.UtcNow, 0)));
                }, exception =>
                {
                    _logger.Error(exception, "Exception in computation task stream");
                    sender.Tell(new UpdateItem<int, Job<string>>(jobId,
                        new Job<string>(jobId, JobStatus.Error, jobId.ToString(), DateTime.UtcNow, 0)));
                }))
                .To(Sink.ForEach<ComputationStreamResult>(computationResult =>
                {
                    completedJobs += computationResult.BatchJobs.Count;
                    totalErrors += computationResult.TotalErrors;
                    if (computationResult.HasError || computationResult.TotalErrors > 0)
                        hasError = true;

                    var progress = Math.Floor((double)completedJobs / totalJobs);
                    var job = new Job<string>(jobId, JobStatus.Processing, jobId.ToString(), DateTime.UtcNow, progress);
                    sender.Tell(new UpdateItem<int, Job<string>>(jobId, job));
              }));

        Source.From(batchJobs)
            .RunWith(sink, materializer);
    }

    private static Task RandomWaitAsync(int min, int max)
        => Task.Delay(TimeSpan.FromMilliseconds(ThreadLocalRandom.Current.Next(min, max)));
    
    private async Task<Source<ComputationResult, NotUsed>> CreateStream(ComputationTask message)
    {
        var batchJobs = message.BatchJobs;
        
        await RandomWaitAsync(50, 120);

        await Task.WhenAll(batchJobs.Select(c => Task.Run(async () =>
        {
            await RandomWaitAsync(50, 120);
        })));

        await RandomWaitAsync(50, 120);

        await Task.WhenAll(batchJobs.Select(c => Task.Run(async () =>
        {
            await RandomWaitAsync(50, 120);
        })));

        await Task.WhenAll(batchJobs.Select(c => Task.Run(async () =>
        {
            var jobs = Enumerable.Range(1, ThreadLocalRandom.Current.Next(50, 100));
            await Task.WhenAll(jobs.Select(existingCalculationId =>
                Task.Run(async () =>
                {
                    foreach (var _ in Enumerable.Range(1, ThreadLocalRandom.Current.Next(10, 50)))
                    {
                        await RandomWaitAsync(50, 120);
                    }
                })));
        })));

        return Source
          .From(batchJobs)
          .Select(bathJob =>
          {
              return Source.From(Enumerable.Range(1, ThreadLocalRandom.Current.Next(50, 100)))
                  .Select(calculationDate =>
                  {
                      return (HasError: false, Id: calculationDate);
                  })
                  .Select(resolverResult => new ComputationResult(resolverResult.HasError,
                      new CalculationResult
                      {
                          Id = resolverResult.Id,
                          BatchJobId = bathJob.BatchId.ToString(),
                      }));
              
          })
          .ConcatMany(x => x);
     }

    public async Task<ComputationStreamResult> WriteStream(ComputationStream message, ActorMaterializer materializer)
    {
        var batch = message.BatchJobs;
        var source = message.Source;
        var totalErrors = 0;
        var hasError = false;

        await Task.WhenAll(batch.Select(c => Task.Run(async () =>
        {
            await RandomWaitAsync(50, 120);
        })));

        if (!hasError)
        {
          try
          {
            await source
              .RunWith(Sink.ForEach<ComputationResult>((result) =>
              {
                if (result.HasError)
                {
                  totalErrors += 1;
                }
              }), materializer);
          }
          catch (Exception ex)
          {
              _logger.Error(ex, "Calculation result writer exception getting new results");
            
            hasError = true;
          }
        }

        if (!hasError)
        {
            try
            {
                await Source
                    .From(Enumerable.Range(1, ThreadLocalRandom.Current.Next(50, 100))
                        .Select(i => (true, new CalculationResult())))
                    .Grouped(25)
                    .RunWith(
                        Sink.ForEachAsync<IEnumerable<(bool, CalculationResult)>>(1, Write),
                        materializer);
                return new ComputationStreamResult(batch, totalErrors, totalErrors > 0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Calculation result writer exception writing results");

                return new ComputationStreamResult(batch, 0, true);
            }
        }

        return new ComputationStreamResult(batch, 0, true);
    }

    private Task Write(IEnumerable<(bool, CalculationResult)> _)
        => RandomWaitAsync(50, 150);
}