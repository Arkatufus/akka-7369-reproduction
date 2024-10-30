using Akka.Actor;
using Akka.Event;

namespace StreamTest;

public class ManagerActor: ReceiveActor, IWithTimers
{
    private int _jobId;
    private readonly IActorRef _compute;
    
    public ManagerActor(IActorRef compute)
    {
        _compute = compute;
        var log = Context.GetLogger();

        Receive<UpdateItem<int, Job<string>>>(msg =>
        {
            var job = msg.Data;
            log.Info($"Job {job.JobId} status: {job.JobStatus}, progress: {job.Progress}");
            if (job.JobStatus is JobStatus.Complete or JobStatus.Error)
                _compute.Tell(new Compute(Enumerable.Range(1, 50).Select(_ => new BatchJobInfo()).ToList(), ++_jobId));
        });

        Receive<Start>(_ =>
        {
            _compute.Tell(new Compute( Enumerable.Range(1, 50).Select(_ => new BatchJobInfo()).ToList(),  ++_jobId));
        });
    }

    public ITimerScheduler Timers { get; set; } = null!;

    protected override void PreStart()
    {
        base.PreStart();
        Self.Tell(new Start());
    }
    
    private class Start { }
}