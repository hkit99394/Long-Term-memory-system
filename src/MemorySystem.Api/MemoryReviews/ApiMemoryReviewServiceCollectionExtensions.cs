using MemorySystem.Api.Events;
using MemorySystem.Application.MemoryReviews;
using MemorySystem.Infrastructure.MemoryReviews;

namespace MemorySystem.Api.MemoryReviews;

public static class ApiMemoryReviewServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemMemoryReviews(this IServiceCollection services)
    {
        services.AddMemorySystemSourceEventLinks();
        services.AddSingleton<IMemoryReviewQueue, MemoryReviewQueue>();
        services.AddSingleton<IMemoryReviewWorkflow, MemoryReviewWorkflow>();
        services.AddSingleton<IMemoryReviewRepository, PostgresMemoryReviewRepository>();
        services.AddSingleton<IMemoryReviewActionIdempotencyResponseSerializer, ApiMemoryReviewActionIdempotencyResponseSerializer>();
        services.AddSingleton<IMemoryReviewActionStore, PostgresMemoryReviewActionStore>();

        return services;
    }
}
