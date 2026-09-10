using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ISlaPolicyValidator
{
    void ValidateForSave(SlaPolicy policy);
}
