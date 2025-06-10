# RecordsMaster  0.12

# When changing the model, or running for the first time, do these things:
0. dotnet ef migrations add InitialMigration 
1. dotnet ef database update
2. dotnet ef migrations add AddIdentityRoleSupport   
3. dotnet ef database update
4. dotnet build
5. dotnet run