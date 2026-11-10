using System.Reflection;
using System.Reflection.Emit;
using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Jobby.Core.Services;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobbyBuilderTests
{
    [Fact]
    public void AddJobsFromAssemblies_SingleCommandHandler_RegistersJob()
    {
        var assembly = BuildTempJobsAssembly(asm =>
        {
            var command = DefineCommand(asm, "TestCommand", "TestJob");
            DefineHandler(asm, "TestHandler", command);
        });
        var builder = new JobbyBuilder();

        builder.AddJobsFromAssemblies(assembly);

        AssertJobAdded(builder, "TestCommand");
    }

    [Fact]
    public void AddJobsFromAssemblies_MultiCommandHandler_RegistersJobForEachCommand()
    {
        var assembly = BuildTempJobsAssembly(asm =>
        {
            var firstCommand = DefineCommand(asm, "FirstCommand", "FirstMultiHandledJob");
            var secondCommand = DefineCommand(asm, "SecondCommand", "SecondMultiHandledJob");
            DefineHandler(asm, "MultiCommandHandler", firstCommand, secondCommand);
        });
        var builder = new JobbyBuilder();

        builder.AddJobsFromAssemblies(assembly);

        AssertJobAdded(builder, "FirstCommand");
        AssertJobAdded(builder, "SecondCommand");
    }

    [Fact]
    public void AddJobsFromAssemblies_CommandWithoutHandler_Throws()
    {
        var assembly = BuildTempJobsAssembly(asm =>
        {
            DefineCommand(asm, "OrphanCommand", "OrphanJob");
        });
        var builder = new JobbyBuilder();

        var exception = Assert.Throws<InvalidJobsConfigException>(() => builder.AddJobsFromAssemblies(assembly));
        Assert.Contains("does not have handler", exception.Message);
    }

    [Fact]
    public void AddJobsFromAssemblies_TwoHandlersForSameCommand_Throws()
    {
        var assembly = BuildTempJobsAssembly(asm =>
        {
            var command = DefineCommand(asm, "TestCommand", "TestJob");
            DefineHandler(asm, "FirstHandler", command);
            DefineHandler(asm, "SecondHandler", command);
        });
        var builder = new JobbyBuilder();

        var exception = Assert.Throws<InvalidJobsConfigException>(() => builder.AddJobsFromAssemblies(assembly));
        Assert.Contains("Each job command must have single handler", exception.Message);
    }

    [Fact]
    public void CreateJobbyServer_WithoutExecutionScopeFactory_ThrowsInvalidBuilderConfigException()
    {
        var builder = new JobbyBuilder();
        builder.UseStorage(new Mock<IJobbyStorage>().Object);

        var exception = Assert.Throws<InvalidBuilderConfigException>(() => builder.CreateJobbyServer());
        Assert.Contains("ExecutionScopeFactory is not specified", exception.Message);
    }

    [Fact]
    public void CreateJobbyServer_WithoutStorage_ThrowsInvalidBuilderConfigException()
    {
        var builder = new JobbyBuilder();
        builder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(new ExecutedCommandsList()));

        var exception = Assert.Throws<InvalidBuilderConfigException>(() => builder.CreateJobbyServer());
        Assert.Contains("Storage is not specified", exception.Message);
    }

    [Fact]
    public void CreateJobbyServer_WithoutPermanentLocksStorage_ThrowsInvalidBuilderConfigException()
    {
        var builder = new JobbyBuilder();
        builder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(new ExecutedCommandsList()));
        builder.UseStorage(new Mock<IJobbyStorage>().Object);

        var exception = Assert.Throws<InvalidBuilderConfigException>(() => builder.CreateJobbyServer());
        Assert.Contains("PermanentLocksStorage is not specified", exception.Message);
    }

    [Fact]
    public void CreateJobbyServer_WithAllComponents_CreatesServerTwiceWithoutErrors()
    {
        var builder = new JobbyBuilder();
        builder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(new ExecutedCommandsList()));
        builder.UseStorage(new Mock<IJobbyStorage>().Object);
        builder.UsePermanentLocksStorage(_ => new Mock<IPermanentLocksStorage>().Object);
        // system outer middlewares are re-registered on every CreateJobbyServer call
        builder.UseMetrics();
        builder.UseTracing();

        var server1 = builder.CreateJobbyServer();
        var server2 = builder.CreateJobbyServer();

        Assert.NotNull(server1);
        Assert.NotNull(server2);
        Assert.NotSame(server1, server2);
    }

    [Fact]
    public void CreateJobbyClient_WithoutStorage_ThrowsInvalidBuilderConfigException()
    {
        var builder = new JobbyBuilder();

        Assert.Throws<InvalidBuilderConfigException>(() => builder.CreateJobbyClient());
    }

    [Fact]
    public void AddJob_DuplicateJobName_ThrowsInvalidJobsConfigException()
    {
        var builder = new JobbyBuilder();

        builder.AddJob<TestJobCommand, TestJobCommandHandler>();

        var exception = Assert.Throws<InvalidJobsConfigException>(
            () => builder.AddJob<TestJobCommand, TestJobCommandHandler>());
        Assert.Contains("has already been added", exception.Message);
    }

    [Fact]
    public void AddJobsFromAssemblies_JobWithSameNameAlreadyAddedViaAddJob_Throws()
    {
        var assembly = BuildTempJobsAssembly(asm =>
        {
            var command = DefineCommand(asm, "TestJobCommand", "TestJob");
            DefineHandler(asm, "TestJobHandler", command);
        });
        var builder = new JobbyBuilder();
        builder.AddJob<TestJobCommand, TestJobCommandHandler>();

        // AddJobsFromAssemblies uses Dictionary.Add, which throws on duplicate job name.
        // Pins the current (arguably rough) behaviour: not a friendly InvalidJobsConfigException
        var exception = Assert.Throws<ArgumentException>(() => builder.AddJobsFromAssemblies(assembly));
        Assert.Contains("TestJob", exception.Message);
    }

    [Fact]
    public void AddOrReplaceJob_DuplicateJobName_ReplacesWithoutThrow()
    {
        var builder = new JobbyBuilder();

        builder.AddJob<TestJobCommand, TestJobCommandHandler>();
        builder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();

        Assert.Single(builder.AddedJobTypes);
    }

    private static void AssertJobAdded(JobbyBuilder builder, string commandTypeName)
    {
        var metadata = builder.AddedJobTypes.FirstOrDefault(x => x.CommandType.Name == commandTypeName);

        Assert.True(metadata.CommandType != null, $"No job registered for command '{commandTypeName}'");
    }

    public class CommandBase : IJobCommand
    {
        public static string GetJobName() => throw new InvalidOperationException("Must be hidden by emitted command types");
    }

    private static Assembly BuildTempJobsAssembly(Action<ModuleBuilder> define)
    {
        var assemblyName = new AssemblyName($"JobbyBuilderTests_{Guid.NewGuid():N}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("JobbyBuilderTestsModule");
        define.Invoke(module);
        return assembly;
    }

    private static Type DefineCommand(ModuleBuilder module, string typeName, string jobName)
    {
        // Reflection.Emit cannot implement static abstract interface members (IJobCommand.GetJobName),
        // so the emitted command inherits the interface implementation from CommandBase
        // and hides its GetJobName with its own static method.
        var typeBuilder = module.DefineType(typeName,
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, typeof(CommandBase));

        var getJobNameMethod = typeBuilder.DefineMethod(nameof(IJobCommand.GetJobName),
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            typeof(string), Type.EmptyTypes);
        var il = getJobNameMethod.GetILGenerator();
        il.Emit(OpCodes.Ldstr, jobName);
        il.Emit(OpCodes.Ret);

        return typeBuilder.CreateType();
    }

    private static Type DefineHandler(ModuleBuilder module, string typeName, params Type[] commandTypes)
    {
        var typeBuilder = module.DefineType(typeName,
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);

        foreach (var commandType in commandTypes)
        {
            var handlerInterface = typeof(IJobCommandHandler<>).MakeGenericType(commandType);
            typeBuilder.AddInterfaceImplementation(handlerInterface);

            var executeMethod = typeBuilder.DefineMethod("ExecuteAsync",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig |
                MethodAttributes.NewSlot | MethodAttributes.Final,
                typeof(Task), [commandType, typeof(JobExecutionContext)]);
            var il = executeMethod.GetILGenerator();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(executeMethod, handlerInterface.GetMethod("ExecuteAsync")!);
        }

        return typeBuilder.CreateType();
    }
}
