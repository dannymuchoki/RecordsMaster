# RecordsMaster  0.1

0. dotnet ef migrations add InitialMigration 
1. dotnet ef database update
2. dotnet ef migrations add AddIdentityRoleSupport   
3. dotnet ef database update
4. dotnet build
5. dotnet run