namespace MemorySystem.Worker;

public static class OutboxWorkerReadiness
{
    public static void ThrowIfCannotStart(OutboxWorkerOptions options, int registeredHandlerCount)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return;
        }

        if (registeredHandlerCount <= 0)
        {
            throw new InvalidOperationException(
                "Outbox worker is enabled, but no outbox job handlers are registered. "
                + "Set OutboxWorker:Enabled=false until a handler is available, or register at least one IOutboxJobHandler.");
        }
    }
}
