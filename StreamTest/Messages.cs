using Akka;
using Akka.Streams.Dsl;

namespace StreamTest;

public class BatchJobInfo
{
    private static int _count;
    
    public int BatchId { get; } = ++_count;
}

public enum JobStatus
{
    Processing,
    Complete,
    Error
}

public class Job<T>
{
    public Job(int jobId, JobStatus jobStatus, T payload, DateTime timestamp, double progress)
    {
        JobId = jobId;
        JobStatus = jobStatus;
        Payload = payload;
        Timestamp = timestamp;
        Progress = progress;
    }

    public int JobId { get; }
    public JobStatus JobStatus { get; }
    public T Payload { get; }
    public DateTime Timestamp { get; }
    public double Progress { get; }
}

public class UpdateItem<TId, TData>
{
    public UpdateItem(TId id, TData data)
    {
        Id = id;
        Data = data;
    }

    public TId Id { get; }
    public TData Data { get; }
}

public class Compute
{
    public Compute(List<BatchJobInfo> batchJobs, int jobId)
    {
        BatchJobs = batchJobs;
        JobId = jobId;
    }

    public List<BatchJobInfo> BatchJobs { get; }
    public int JobId { get; }
}

public class ComputationTask
{
    public ComputationTask(int jobId, List<BatchJobInfo> batchJobs)
    {
        JobId = jobId;
        BatchJobs = batchJobs;
    }

    public int JobId { get; }
    public List<BatchJobInfo> BatchJobs { get; }
}

public class ComputationStream
{
    public ComputationStream(int jobId, List<BatchJobInfo> batchJobs, Source<ComputationResult, NotUsed> source)
    {
        JobId = jobId;
        BatchJobs = batchJobs;
        Source = source;
    }

    public int JobId { get; }
    public List<BatchJobInfo> BatchJobs { get; }

    public Source<ComputationResult, NotUsed> Source { get; }
}

public class ComputationStreamResult
{
    public ComputationStreamResult(List<BatchJobInfo> batchJobs, int totalErrors, bool hasError)
    {
        BatchJobs = batchJobs;
        TotalErrors = totalErrors;
        HasError = hasError;
    }

    public List<BatchJobInfo> BatchJobs { get; }
    public int TotalErrors { get; }
    public bool HasError { get; }
}

public class ComputationResult
{
    public ComputationResult(bool hasError, CalculationResult result)
    {
        HasError = hasError;
        Result = result;
    }

    public bool HasError { get; }
    public CalculationResult Result { get; }
}

public class CalculationResult
{
    public int Id { get; init; }
    public string BatchJobId { get; init; }
}