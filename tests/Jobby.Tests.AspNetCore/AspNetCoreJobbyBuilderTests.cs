using Jobby.AspNetCore;
using Jobby.Core.Interfaces.Configuration;
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
}
