# RecordsMaster  0.90

# Before anything rename 'appsettings-prod.json' to 'appsettings.json'
0. Rename 'appsettings-prod.json' filename to 'appsettings.json' - this is a template appsettings file.
1. If you like, modify the default admin and user parameters in the 'UserSeedData' key
2. You will need to enter a SMTP server in appsettings for email to work (this is untested)

# When changing the model, or running for the first time, do these things:
0. dotnet ef migrations add InitialMigration 
1. dotnet ef database update
2. dotnet ef migrations add AddIdentityRoleSupport   
3. dotnet ef database update
4. dotnet build
5. dotnet run
