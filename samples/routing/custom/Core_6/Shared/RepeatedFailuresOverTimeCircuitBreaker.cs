using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;

class RepeatedFailuresOverTimeCircuitBreaker :
    IDisposable,
    ICircuitBreaker
{
    public RepeatedFailuresOverTimeCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering, Action<Exception> triggerAction)
    {
        this.name = name;
        this.triggerAction = triggerAction;
        this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;

        timer = new Timer(CircuitBreakerTriggered);
    }

    public void Success()
    {
        var oldValue = Interlocked.Exchange(ref failureCount, 0);

        if (oldValue == 0)
        {
            return;
        }

        timer.Change(Timeout.Infinite, Timeout.Infinite);
        log.InfoFormat("The circuit breaker for {0} is now disarmed", name);
    }

    public Task Failure(Exception exception)
    {
        lastException = exception;
        var newValue = Interlocked.Increment(ref failureCount);

        if (newValue == 1)
        {
            timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
            log.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
        }

        return Task.Delay(TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        //Injected
    }

    void CircuitBreakerTriggered(object state)
    {
        if (Interlocked.Read(ref failureCount) > 0)
        {
            log.WarnFormat("The circuit breaker for {0} will now be triggered", name);
            triggerAction(lastException);
        }
    }

    long failureCount;
    Exception lastException;

    string name;
    Timer timer;
    TimeSpan timeToWaitBeforeTriggering;
    Action<Exception> triggerAction;

    static TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
    static ILog log = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();
}