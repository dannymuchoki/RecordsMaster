# RecordsMaster 
**RecordsMaster** is a simple CRUD (Create, Read, Update, Delete) application made in .NET. I made for a records room at my place of work. It replaces an Access database. I made it because I could not find anything on the market that met the records room's needs. I made the code public so that others who have similar challenges can reuse it for their purposes. 

# Before anything rename 'appsettings-prod.json' to 'appsettings.json'
0. Rename 'appsettings-prod.json' filename to 'appsettings.json' - this is a template appsettings file.
1. Modify the default admin and user parameters in the 'UserSeedData' key in appsettings. 
2. Add the email boxes you'd like to use in appsettings. 
2. The default admin user in appsettings under the "AdminEmail" key is referenced the UserRoles.cshtml view. This is done to avoid accidental self-nerfing the admin user. This user will see "this user's permissions cannot be modified".
3. You will need to enter a SMTP server in appsettings for email to work.

## Appsettings holds most of the configurations you need to change.
You can use it to set up the admin users, mailboxes, how many pages the List view should have, and the database credentials.

# Rename 'original_data_template.csv' to 'file.csv' if you want to populate the table at initial migration.
0. I **highly recommend** populating the data at initial migration when you have a lot of data. If you include which user's email address has a record checked out at initial migration, RecordsMaster will create a user and associate the record with them. This user will need to reset their password to get access to their account. See the original_data_template.csv file's 'CheckedOutTo' column.  
1. Otherwise, the seeded data in Program.cs will populate the table. 

# Set the development environment to Production or Development
0. You can do this in launchsettings.json, or in the web.config

## In Development mode:
Migrations will create the SQLite database with the admin user, a test user, and the seeded information. 

If in Development, make sure to uncomment this in RecordsMaster.csjproj.   

 ```   
  <Content Include="testdb.db">
      <CopyToPublishDirectory>Always</CopyToPublishDirectory>
  </Content>
```
This will ensure the Development SQLite database (testdb.db) is included in your published directory. Remove this in Production.

## In Production mode:
The database will be SQL, and the migrations will run using your database credentials. Ensure you have the ability to run migrations. Follow your administrator's rules on securing database credentials. I recommend against keeping them in appsettings generally.   

# When changing the model, or running for the first time, do these things:
0. dotnet ef migrations add InitialMigration 
1. dotnet ef database update
2. dotnet ef migrations add AddIdentityRoleSupport   
3. dotnet ef database update
4. dotnet build
5. dotnet watch run (tracks live changes)

Consider creating a shell script for the first five commands. In bash it's something like:

```
#!/bin/bash
# Save this as a setup.sh file and chmod+x it
set -e # makes sure errors kill setup.

dotnet ef migrations add InitialMigration
dotnet ef database update
dotnet ef migrations add AddIdentityRoleSupport
dotnet ef database update
dotnet build
```

## To publish and deploy:

> dotnet publish -c Release -o ./publish

This will put the binaries in a /publish directory right in the root directory. If you are mapped to a remote server, you can publish to that remote server. 

# App overall
The app runs on two models and the default ApplicationUser. 
1. RecordItemsModel tied to the default ApplicationUser. 
2. CheckOutHistory which tracks when a record was checked in and out
3. The default ApplicationUser (so you don't need to create a model for the users)

The app has ten controllers. The controllers, as the name suggests, control what the user sees in the views. Each controller has a corresponding view in the 'Views' directory. Each controller has different methods.

Admin users can see what each user has requested or checked out via the 'Manage Users' page. Click on the username hyperlink to access the user's record view.

## 'Services' directory contains:
1. The email sender. This is flexible and may require configuration to work with your SMTP setup. 
2. Two PDF printing services - one for development in Windows, the other cross-plaform. The development version is not used in the final app; it's just there to help troubleshoot.

## 'Utilities' directory contains:
1. A class that reads CSV files
2. A class that implements rudimentary pagination in the 'List' view of all records. Pagination is controlled by a key in the appsettings.json file 

## 'Data' directory contains:
1. This is where you can find AppDbContext - this is mostly ORM stuff that runs at initial migration. 
2. 'SeedTestData' used to be part of AppDbContext. Now it sets up the admin users and the first seeded data. 
3. 'CsvHelperMap' helps map uploaded .csv data to the model. You can use it to reconstruct the order of columns in the .csv file. It is also easier than managing the data in the controllers. This feels more like a utility but oh well. 
 
 For .NET 10, update these in .csproj

Microsoft.AspNetCore.Identity.EntityFrameworkCore	9.0.1
Microsoft.AspNetCore.Identity.UI	9.0.1
Microsoft.EntityFrameworkCore	9.0.1
Microsoft.EntityFrameworkCore.Design	9.0.1
Microsoft.EntityFrameworkCore.Relational	9.0.1
Microsoft.EntityFrameworkCore.Sqlite	9.0.1
Microsoft.EntityFrameworkCore.SqlServer	9.0.1
Microsoft.EntityFrameworkCore.Tools	9.0.1
System.Drawing.Common	9.0.7