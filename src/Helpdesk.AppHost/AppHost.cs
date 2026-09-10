var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Helpdesk_API>("helpdesk-api");

builder.AddProject<Projects.HelpDesk_NewWeb>("helpdesk-newweb");

builder.Build().Run();
