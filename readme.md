# RecordsMaster  1.5

# Before anything rename 'appsettings-prod.json' to 'appsettings.json'
0. Rename 'appsettings-prod.json' filename to 'appsettings.json' - this is a template appsettings file.
1. Modify the default admin and user parameters in the 'UserSeedData' key. 
2. The default admin user is referenced the UserRoles.cshtml view. This is done to avoid accidental self-nerfing the admin. Admin user will see "this user's permissions cannot be modified".
3. You will need to enter a SMTP server in appsettings for email to work (this is untested).

# Rename 'original_data_template.csv' to 'file.csv' if you want to populate the table at initial migration.
0. Otherwise, the seeded data in Program.cs will populate the table. 

# When changing the model, or running for the first time, do these things:
0. dotnet ef migrations add InitialMigration 
1. dotnet ef database update
2. dotnet ef migrations add AddIdentityRoleSupport   
3. dotnet ef database update
6. dotnet build
7. dotnet watch run (tracks live changes)

To publish:

> dotnet publish -c Release -o ./publish

This will put the binaries in a /publish directory right in the root directory. 

If in development, make sure to uncomment this in RecordsMaster.csjproj.   

 ```   
  <Content Include="testdb.db">
      <CopyToPublishDirectory>Always</CopyToPublishDirectory>
  </Content>
```

This will create the SQLite database with the admin user, a test user, and the seeded information. Check the ASPNETCORE_ENVIRONMENT variables in launchSettings.json. When in 'Development' the default database is SQLite. When in 'Production' the database will be SQL. 

# App overall
The app runs on two models (and the default ApplicationUser) 
1. RecordItemsModel tied to the default ApplicationUser. 
2. CheckOutHistory which tracks when a record was checked in and out
3. The default ApplicationUser (so you don't need to create a model for the users)

The app has ten controllers. The controllers, as the name suggests, control what the user sees in the views. Each controller has a corresponding view in the 'Views' directory. 

Admin users can see what each user has requested or checked out via the 'Manage Users' page. Click on the username hyperlink to access the user's record view.

## 'Services' directory contains:
1. The untested email sender classes.
2. Two PDF printing services - one for development in Windows, the other cross-plaform. 

## 'Utilities' directory contains:
1. A class that reads CSV files
2. A class that implements rudimentary pagination in the 'List' view of all records. Pagination is controlled by a key in the appsettings.json file 

## 'Data' directory contains:
1. I'll be honest, other than AppDbContext, I can't remember why I split Utilities and Data.
2. This is where you can find AppDbContext - this is mostly ORM stuff that runs at initial migration. 
3. 'SeedTestData' used to be part of AppDbContext. Now it sets up the admin users and the first seeded data. 
4. 'CsvHelperMap' helps map uploaded .csv data to the model. You can use it to reconstruct the order of columns in the .csv file. It is also easier than managing the data in the controllers. This feels more like a utility but oh well. 
 