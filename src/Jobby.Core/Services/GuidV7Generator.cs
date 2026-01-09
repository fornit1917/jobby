using Jobby.Core.Interfaces;
using UUIDNext;

namespace Jobby.Core.Services;

internal class GuidV7Generator : IGuidGenerator
{
    public Guid NewGuid()
    {
        return Uuid.NewSequential();
    }
}