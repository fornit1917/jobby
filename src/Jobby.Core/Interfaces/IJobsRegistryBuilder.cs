namespace Jobby.Core.Interfaces;

public interface IJobsRegistryBuilder
{
    IJobsRegistryBuilder AddJob<TCommand, THandler>() 
        where TCommand : IJobCommand
        where THandler : IJobCommandHandler<TCommand>;

    IJobsRegistryBuilder AddRecurrentJob<THandler>() where THandler : IRecurrentJobHandler;

    IJobsRegistry Build();
}
