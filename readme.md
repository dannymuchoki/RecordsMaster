# RecordsMaster  1.2

# Before anything rename 'appsettings-prod.json' to 'appsettings.json'
0. Rename 'appsettings-prod.json' filename to 'appsettings.json' - this is a template appsettings file.
1. If you like, modify the default admin and user parameters in the 'UserSeedData' key. 
2. The default admin user is referenced the UserRoles.cshtml view. This is done to avoid accidental self-nerfing!
3. You will need to enter a SMTP server in appsettings for email to work (this is untested)

# Rename 'original_data_template.csv' to 'original_data.csv' if you want to populate the table at initial migration.
0. Otherwise, the seeded data in Program.cs will populate the table. 

# When changing the model, or running for the first time, do these things:
0. dotnet ef migrations add InitialMigration 
1. dotnet ef database update
2. dotnet ef migrations add AddIdentityRoleSupport   
3. dotnet ef database update
4. dotnet build
5. dotnet run

This will create the SQLite database with the admin user, a test user, and the seeded information. Check the ASPNETCORE_ENVIRONMENT variables in launchSettings.json. When in 'Development' the default database is SQLite. When in 'Production' the database will be SQL. 

# App overall
The app runs on one model (RecordItemsModel) tied to the default ApplicationUser. It has ten controllers. The controllers, as the name suggests, control what the user sees in the views.

Note that, other than the PasswordReset controller, each controller has a corresponding view in the 'Views' directory. 

Admin users can see what each user has requested or checked out via the 'Manage Users' page. Click on the username hyperlink to access the user's record view.

## 'Services' directory contains:
1. The untested email sender classes.
2. Two PDF printing services - one for development in Windows, the other cross-plaform. 

## 'Utilities' directory contains:
1. A class that reads CSV files
2. A class that implements rudimentary pagination in the 'List' view of all records. Pagination is controlled by a key in the appsettings.json file 

## Password Resets:
- The password reset controller checks if a user's email address is registered. If so, it generates a new strong password for the user's account and sends it to the user via email. In testing mode when you may not have a SMTP server set up, the email will print in console. 