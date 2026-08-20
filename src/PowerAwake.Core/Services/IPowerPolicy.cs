using PowerAwake.Core.Models;

namespace PowerAwake.Core.Services;

public interface IPowerPolicy
{
    Guid GetActiveScheme();
    PowerValues ReadValues(Guid schemeGuid);
    void WriteValues(Guid schemeGuid, PowerValues values, bool activate = true);
}