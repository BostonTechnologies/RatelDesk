using System.Text.Json;
using Helpdesk.Shared.DTOs.RequestForm;

namespace Helpdesk.Application.RequestTasks;

public interface IRequestFormSchemaParser
{
    RequestFormSchemaModel Parse(JsonDocument schema);
}
