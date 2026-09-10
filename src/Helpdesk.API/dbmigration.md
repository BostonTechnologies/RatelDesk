Correct Command

From solution root:

dotnet ef migrations add AddOrchestrationClientIdToM2MConnectivitySettings --project Helpdesk.Infrastructure --startup-project Helpdesk.API


--project → where the DbContext & migrations live
--startup-project → which project provides configuration (connection string, DI)

Your API project is the startup host, so it must be specified.

Then Apply Migration
dotnet ef database update --project Helpdesk.Infrastructure --startup-project Helpdesk.API