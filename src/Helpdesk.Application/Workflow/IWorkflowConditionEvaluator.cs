using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Workflow;

public interface IWorkflowConditionEvaluator
{
    bool Evaluate(Request request, RequestTask task);
}
