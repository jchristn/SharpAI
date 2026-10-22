namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using SharpAI.Database;
    using SharpAI.Database.Sqlite;
    using SharpAI.Models;
    using SharpAI.Security;
    using SyslogLogging;
    using Xunit;

    /// <summary>Private-library visibility, portable storage, and generation parameter contracts.</summary>
    public sealed class LibraryTests
    {
        /// <summary>Administrators cannot accidentally expose another account's private records.</summary>
        [Fact]
        public void OwnershipIsSeparateFromPublicationPrivileges()
        {
            RequestContext user=new RequestContext {IsAuthenticated=true,PrincipalType=PrincipalTypeEnum.User,PrincipalGuid="user-a",TenantGuid="tenant-a"};
            LibraryItem system=new LibraryItem {Scope="System"};
            LibraryItem tenant=new LibraryItem {Scope="Tenant",TenantId="tenant-a"};
            LibraryItem own=new LibraryItem {Scope="User",TenantId="tenant-a",UserId="user-a"};
            LibraryItem other=new LibraryItem {Scope="User",TenantId="tenant-a",UserId="user-b"};
            Assert.True(LibraryAccess.CanRead(system,user,"tenant-a"));
            Assert.True(LibraryAccess.CanRead(tenant,user,"tenant-a"));
            Assert.True(LibraryAccess.CanEdit(own,user));
            Assert.False(LibraryAccess.CanRead(other,user,"tenant-a"));
            Assert.False(LibraryAccess.CanEdit(tenant,user));
            user.IsTenantAdmin=true;
            Assert.True(LibraryAccess.CanEdit(tenant,user));
            Assert.False(LibraryAccess.CanEdit(system,user));
            Assert.False(LibraryAccess.CanEdit(new LibraryItem {Scope="Tenant",TenantId="tenant-b"},user));
            user.IsAdmin=true;
            Assert.True(LibraryAccess.CanEdit(system,user));
            Assert.False(LibraryAccess.CanRead(other,user,"tenant-a"));
            RequestContext open=new RequestContext {IsAuthenticated=true,IsAdmin=true,PrincipalType=PrincipalTypeEnum.System};
            Assert.False(LibraryAccess.CanRead(own,open,"tenant-a"));
            Assert.False(LibraryAccess.CanEdit(own,open));
        }
        /// <summary>Invalid sampling values and prompt fields never become stored generation parameters.</summary>
        [Fact]
        public void GenerationContractValidatesEveryNumericFamily()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new GenerationConfiguration {Temperature=Double.NaN});
            Assert.Throws<ArgumentOutOfRangeException>(()=>new GenerationConfiguration {TopP=1.1});
            Assert.Throws<ArgumentOutOfRangeException>(()=>new GenerationConfiguration {MaxTokens=0});
            Assert.Throws<ArgumentOutOfRangeException>(()=>new GenerationConfiguration {PresencePenalty=-3});
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<GenerationConfiguration>("{\"SystemPrompt\":\"do not accept\"}"));
            GenerationConfiguration config=new GenerationConfiguration {Temperature=.7,MaxTokens=32768,TopP=.8,TopK=0,MinP=0,RepeatPenalty=2,RepeatLastN=0,PresencePenalty=-2,FrequencyPenalty=2,Seed=Int32.MaxValue,StreamResponse=false,JsonOutput=true,DisplayThinking=true};
            GenerationConfiguration loaded=JsonSerializer.Deserialize<GenerationConfiguration>(JsonSerializer.Serialize(config))!;
            Assert.Equal(config.Seed,loaded.Seed);Assert.False(loaded.StreamResponse);Assert.True(loaded.JsonOutput);Assert.True(loaded.DisplayThinking);
        }
        /// <summary>Deleting an account removes its private library while tenant defaults survive until the tenant is deleted.</summary>
        [Fact]
        public async Task ScopeLifecycleCleanupPreservesSharedSystemDefaults()
        {
            string filename=Path.Combine(Path.GetTempPath(),"sharpai-library-owner-"+Guid.NewGuid().ToString("N")+".db");
            try
            {
                using(LoggingModule logging=new LoggingModule())
                using(SqliteDatabaseDriver db=new SqliteDatabaseDriver(new DatabaseSettings {Filename=filename},logging))
                {
                    await db.InitializeAsync();
                    Tenant tenant=db.Tenants.Create(new Tenant {Name="Library owner"});
                    User user=db.Users.Create(new User {TenantGuid=tenant.Guid,Email="owner@example.test"});
                    LibraryItem personal=db.Library.Create(new LibraryItem {Kind="SystemPrompt",Name="Personal",Scope="User",TenantId=tenant.Guid,UserId=user.Guid,Content="Personal"});
                    LibraryItem shared=db.Library.Create(new LibraryItem {Kind="SystemPrompt",Name="Shared",Scope="Tenant",TenantId=tenant.Guid,Content="Shared"});
                    db.Users.Delete(user.Guid);Assert.Null(db.Library.Read(personal.Id));Assert.NotNull(db.Library.Read(shared.Id));
                    db.Tenants.Delete(tenant.Guid);Assert.Null(db.Library.Read(shared.Id));Assert.NotNull(db.Library.Read("system-starter-prompt"));
                }
            }
            finally {if(File.Exists(filename))File.Delete(filename);}
        }

        /// <summary>SQL visibility filtering happens before totals and paging, and survives opening a second driver.</summary>
        [Fact]
        public async Task MigrationAndPrivatePaginationSurviveReopen()
        {
            string filename=Path.Combine(Path.GetTempPath(),"sharpai-library-"+Guid.NewGuid().ToString("N")+".db");
            try
            {
                using(LoggingModule logging=new LoggingModule())
                {
                    string id;
                    using(SqliteDatabaseDriver db=new SqliteDatabaseDriver(new DatabaseSettings {Filename=filename},logging))
                    {
                        await db.InitializeAsync();
                        db.Library.Create(new LibraryItem {Kind="SystemPrompt",Name="Tenant",Scope="Tenant",TenantId="a",Content="Tenant text"});
                        LibraryItem own=db.Library.Create(new LibraryItem {Kind="SystemPrompt",Name="Own",Scope="User",TenantId="a",UserId="one",Content="Private text"});id=own.Id;
                        db.Library.Create(new LibraryItem {Kind="SystemPrompt",Name="Hidden",Scope="User",TenantId="a",UserId="two",Content="Hidden text"});
                        db.Library.Create(new LibraryItem {Kind="SystemPrompt",Name="Foreign",Scope="Tenant",TenantId="b",Content="Foreign text"});
                        EnumerationResult<LibraryItem> first=db.Library.Enumerate("SystemPrompt","a","a","one",new EnumerationQuery {PageSize=1});
                        Assert.Equal(3,first.TotalRecords);Assert.Single(first.Objects);Assert.Equal(2,first.RecordsRemaining);
                        Assert.Equal(3,Enumerable.Range(1,3).SelectMany(page=>db.Library.Enumerate("SystemPrompt","a","a","one",new EnumerationQuery {PageSize=1,PageNumber=page}).Objects).Select(item=>item.Id).Distinct().Count());
                        Assert.Equal(2,db.Library.Enumerate("SystemPrompt","a",null,null,new EnumerationQuery()).TotalRecords);
                    }
                    using(SqliteDatabaseDriver db=new SqliteDatabaseDriver(new DatabaseSettings {Filename=filename},logging))
                    {
                        await db.InitializeAsync();Assert.Equal("Private text",db.Library.Read(id).Content);
                        Assert.Single(db.Library.Enumerate("ModelConfiguration","a","a","one",new EnumerationQuery()).Objects);
                        LibraryItem item=db.Library.Read(id);item.Content="Updated";db.Library.Update(item);Assert.Equal("Updated",db.Library.Read(id).Content);
                        db.Library.Delete(id);Assert.Null(db.Library.Read(id));
                    }
                }
            }
            finally {if(File.Exists(filename))File.Delete(filename);}
        }
    }
}
