using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class EmptyStorageMigrator : IJobbyStorageMigrator
{
    public void Migrate()
    {
    }
}