using MemorySystem.Application.MemoryContext;

namespace MemorySystem.Application.Operations;

public interface IContextProductHealthMetricStore
{
    void RecordContextPacket(MemoryContextPacket packet);

    void RecordContextFeedbackAction(string feedbackType);

    void RecordContextReviewOpen(string feedbackType, bool created);

    OperationalContextProductRuntimeSummary ReadRuntimeSummary();
}
