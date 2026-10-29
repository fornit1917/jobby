using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using Jobby.Core.Services;
using Moq;

namespace Jobby.Tests.AspNetCore;

public class AspNetCoreJobbyBuilderTests
{
    [Fact]
    public void ApplyJobbyComponentsConfigureAction_CallsConfigureActionAndPassesServiceProvider()
    {
        var builder = new AspNetCoreJobbyBuilder();
        IServiceProvider? passedServiceProvider = null;
        IServiceProvider serviceProvider = new Mock<IServiceProvider>().Object;
        IJobbyComponentsConfigurable? passedComponentsConfigurable = null;
        Action<IServiceProvider, IJobbyComponentsConfigurable> configure = (sp, opts) =>
        {
            passedServiceProvider = sp;
            passedComponentsConfigurable = opts;
        };

        builder.ConfigureJobby(configure);
        builder.ApplyJobbyComponentsConfigureAction(serviceProvider);

        Assert.Equal(serviceProvider, passedServiceProvider);
        Assert.Equal(builder.JobbyBuilder, passedComponentsConfigurable);
    }

    [Fact]
    public void ConfigureJobby_WithoutServiceProvider_CallsConfigureAction()
    {
        var builder = new AspNetCoreJobbyBuilder();
        IJobbyComponentsConfigurable? passedComponentsConfigurable = null;
        Action<IJobbyComponentsConfigurable> configure = (opts) =>
        {
            passedComponentsConfigurable = opts;
        };

        builder.ConfigureJobby(configure);

        Assert.Equal(builder.JobbyBuilder, passedComponentsConfigurable);
    }

    [Fact]
    public void ConfigureJobby_WithServiceProvider_CalledMultipleTimes_AppliesAllActionsInOrder()
    {
        var builder = new AspNetCoreJobbyBuilder();
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var callLog = new List<string>();

        builder.ConfigureJobby((sp, cfg) => callLog.Add("first"));
        builder.ConfigureJobby((sp, cfg) => callLog.Add("second"));
        builder.ConfigureJobby((sp, cfg) => callLog.Add("third"));

        builder.ApplyJobbyComponentsConfigureAction(serviceProvider);

        Assert.Equal(["first", "second", "third"], callLog);
    }

    [Fact]
    public void ConfigureJobby_WithServiceProvider_DoesNothingUntilApplied()
    {
        var builder = new AspNetCoreJobbyBuilder();
        var applied = false;

        builder.ConfigureJobby((sp, cfg) => applied = true);

        Assert.False(applied);

        builder.ApplyJobbyComponentsConfigureAction(new Mock<IServiceProvider>().Object);

        Assert.True(applied);
    }

    [Fact]
    public void ConfigureJobby_WithServiceProvider_SecondCallDoesNotOverrideFirst()
    {
        var builder = new AspNetCoreJobbyBuilder();
        var firstApplied = false;
        var secondApplied = false;

        builder.ConfigureJobby((sp, cfg) => firstApplied = true);
        builder.ConfigureJobby((sp, cfg) => secondApplied = true);

        builder.ApplyJobbyComponentsConfigureAction(new Mock<IServiceProvider>().Object);

        Assert.True(firstApplied);
        Assert.True(secondApplied);
    }

    [Fact]
    public void ConfigureJobby_BothOverloads_ComposeAndDeferredSeesImmediateEffects()
    {
        var builder = new AspNetCoreJobbyBuilder();
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var deferredSeesImmediateEffects = false;

        builder.ConfigureJobby(cfg => cfg.UseExecutionScopeFactory(new Mock<IJobExecutionScopeFactory>().Object));
        builder.ConfigureJobby((sp, cfg) => deferredSeesImmediateEffects = ((JobbyBuilder)cfg).IsExecutionScopeFactorySpecified);

        builder.ApplyJobbyComponentsConfigureAction(serviceProvider);

        Assert.True(deferredSeesImmediateEffects);
        Assert.True(builder.JobbyBuilder.IsExecutionScopeFactorySpecified);
    }
}
