# RecordsMaster 
**RecordsMaster** is a simple CRUD (Create, Read, Update, Delete) application made in .NET (dotnet) for a records room at my place of work. It replaces an Access database. 

[dotnet](https://dotnet.microsoft.com/en-us/) is sort of like [Flask](https://flask.palletsprojects.com/en/stable/), except made by Microsoft. Instead of Python, you use C#. 

I made RecordsMaster because I could not find anything on the market that met the records room's needs. I made the code public so that others who have similar challenges can reuse it for their purposes. This is partially a learning experience for me, and the best way to learn is to just go for it.  

# Before anything rename 'appsettings-prod.json' to 'appsettings.json'
0. Rename 'appsettings-prod.json' filename to 'appsettings.json' - this is a template appsettings file.
1. Modify the default admin and user email addresses in the 'UserSeedData' key in appsettings. 
2. Add the email boxes you'd like to use in appsettings. 
2. The default admin user in appsettings under the "AdminEmail" key is referenced the UserRoles.cshtml view. This is done to avoid accidental self-nerfing the admin user. This user will see "this user's permissions cannot be modified".
3. You will need to enter a SMTP server in appsettings for email to work. SMTP uses MailKit. 

# Appsettings holds most of the configurations you need to change.
You can use it to set up the admin users, mailboxes, how many pages the List view should have, and the database credentials. 

.NET supports having a Production appsettings (appsettings.Production.json) and a Development appsettings (appsettings.Development.json). I didn't do that here because the only environmental difference in RecordsMaster is the database. 

# Rename 'original_data_template.csv' to 'file.csv' if you want to populate the table at initial migration.
RecordsMaster will populate your database at first run with whatever data you put in file.csv. If you include which user's email address has a record checked out at initial migration, RecordsMaster will create a user and associate the record with them. 

This user will need to reset their password to get access to their account. See the original_data_template.csv file's 'CheckedOutTo' column.  

Otherwise, the seeded data in Program.cs will populate the table. You can use the upload template to add more record items. 

# Set the development environment to Production or Development
0. You can do this in launchsettings.json (for local development), or in the web.config (for production servers). 

# In Development mode:
Migrations will create the SQLite database with the admin user, a test user, and the seeded information. 

Migrations in Development:

> dotnet ef migrations add MyChange --context SqliteAppDbContext

You need the --context flag because .NET doesn't read environment variables when checking the database context (i.e. it needs you to explicitly say which database to use). The SQLite and SQL contexts each have context factory classes in the 'Data' directory that run at design time. This solves a type issue
that kept coming up when I just had two different AppDbContexts. 

.NET **does** read environment variables when loading or running the app in Program.cs though. 

In Development, the migrations will be in a subdirectory of 'Migrations' called 'SQLite'

If in Development, make sure to uncomment this in RecordsMaster.csproj.   

 ```   
  <Content Include="testdb.db">
      <CopyToPublishDirectory>Always</CopyToPublishDirectory>
  </Content>
```
This will ensure the Development SQLite database (testdb.db) is included in your published directory. Remove this in Production because your database will be remote. 

# In Production mode:
The database will be SQL, and the migrations will run using your database credentials. Ensure you have the ability to run migrations. Follow your administrator's rules on securing database credentials. I recommend against keeping them in appsettings generally.   

# SQL Server (production)
> dotnet ef migrations add MyChange --context AppDbContext

The migrations will be in the root 'Migrations' directory. 

# When running for the first time, do these things:
0. dotnet ef migrations add InitialMigration --context AppDbContext (or SqliteAppDbContext)
1. dotnet ef database update  --context AppDbContext (or SqliteAppDbContext)
2. dotnet ef migrations add AddIdentityRoleSupport  --context AppDbContext (or SqliteAppDbContext)  
3. dotnet ef database update --context AppDbContext (or SqliteAppDbContext)
4. dotnet build
5. dotnet watch run (tracks live changes)

Consider creating shell scripts and naming them very differently. DevelopmentSetup.txt has examples you can use. 

# To publish and deploy:

> dotnet publish -c Release -o ./publish

This will put the binaries in a /publish directory right in the root directory. If you are mapped to a remote server, you can publish to that remote server. 

# App overall
The app runs on two models and the default ApplicationUser. 
1. RecordItemsModel tied to the default ApplicationUser. 
2. CheckOutHistory which tracks when a record was checked in and out
3. The default ApplicationUser (so you don't need to create a model for the users)


## Controllers
The app has ten controllers. The controllers, as the name suggests, control what the user sees in the views. Each controller has a corresponding view in the 'Views' directory. Each controller has different methods (or things that you can do with the data in the database)

Admin users can see what each user has requested or checked out via the 'Manage Users' page. Click on the username hyperlink to access the user's record view. There is also a method that will wipe the database, but you must manually configure it. It is only useful in testing and activating it is left as an exercise for the reader (i.e. me when I forget how to call methods in .NET). 

## 'Services' directory contains:
1. The email sender. This is flexible and may require configuration to work with your SMTP setup. I recommend spending several hours with the C# [SMTP class documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient?view=net-9.0). Then ragequit and decide to use [MailKit](https://github.com/jstedfast/mailkit). In fact, that's what the .NET documentation tells you to do. 
2. Three PDF printing services:
   - LabelPrintServices is for development in Windows
   - PDFSharpCorePrintService is cross-platform, but PDFSharpCore has a security vulnerability. It is excluded in both .csproj files, but still around because the PDF labels were really hard to figure out.
   - PDFPrintService is cross-platform and compatible with .NET9 and .NET10.

## 'Utilities' directory contains:
1. A class that reads CSV files
2. A class that implements rudimentary pagination in the 'List' view of all records. Pagination is controlled by a key in the appsettings.json file. 

## 'Data' directory contains:
1. This is where you can find the two AppDbContexts. One for SQLite and the other for Production SQL. That's why there's two different ways to run migrations for Development and Production, and why you need the --context flag. 
2. 'SeedTestData' used to be part of AppDbContext. Now it sets up the admin users and the first seeded data. 
3. 'CsvHelperMap' helps map uploaded .csv data to the model. You can use it to reconstruct the order of columns in the .csv file. It is also easier than managing the data in the controllers. This feels more like a utility but oh well. 
 
# Coming changes
.NET 9 reaches EOL in November 2026. The app is now defaulting to .NET10 in RecordsMaster.csproj. To run in .NET 9:

> dotnet build RecordsMaster.Net9.csproj

If you have feedback, you can reach me via my [website](https://dannymuchoki.com).