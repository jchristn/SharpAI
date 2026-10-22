namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using SharpAI.Models;
    using SharpAI.Security;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Scoped reusable system prompts and model configurations. List, read, write and duplicate all enforce ownership on the server.</summary>
    public static class LibraryRoutes
    {
        /// <summary>Register the library contract on the application server.</summary>
        public static void Register(RouteContext ctx)
        {
            ctx.Server.Get("/v1.0/library-context", async req =>
            {
                RequestContext actor = Actor(req);
                string tenant = Tenant(ctx, req, actor);
                return new { IsAdmin=actor.IsAdmin, IsTenantAdmin=actor.IsTenantAdmin, TenantId=tenant, UserId=LibraryAccess.UserId(actor), UserTenantId=actor.TenantGuid };
            }, api => Meta(api, "Library", "Read library scope and publication rights"));
            RegisterKind(ctx, "system-prompts", "SystemPrompt");
            RegisterKind(ctx, "model-configurations", "ModelConfiguration");
        }
        private static void RegisterKind(RouteContext ctx, string path, string kind)
        {
            string url = "/v1.0/" + path;
            ctx.Server.Get(url, async req =>
            {
                RequestContext actor=Actor(req);
                ctx.Auth.AuthorizeLibrary(req, kind, OperationTypeEnum.Read, null);
                string tenant=Tenant(ctx,req,actor);
                EnumerationQuery query=new EnumerationQuery(); query.ApplyQuerystringOverrides(key=>System.Net.WebUtility.UrlDecode(req.Http.Request.Query.Elements?[key]));
                if((long)(query.PageNumber-1)*query.PageSize>Int32.MaxValue)throw Bad("Page offset is too large.");
                if((query.Name?.Length ?? 0)>256)throw Bad("Name filter exceeds 256 characters.");
                EnumerationResult<LibraryItem> result=ctx.Database.Library.Enumerate(kind,tenant,actor.TenantGuid,LibraryAccess.UserId(actor),query,ctx.Auth.LibraryReadExclusions(req,kind));
                foreach(LibraryItem item in result.Objects)
                {
                    item.CanEdit=LibraryAccess.CanEdit(item,actor)&&ctx.Auth.CanUseLibrary(req,kind,OperationTypeEnum.Update,item.Id);
                    item.CanDelete=LibraryAccess.CanEdit(item,actor)&&ctx.Auth.CanUseLibrary(req,kind,OperationTypeEnum.Delete,item.Id);
                }
                return result;
            }, api => Meta(api, kind, "List visible " + path).WithDescription("System defaults, selected tenant defaults and only the caller's private items. Query: tenantId (platform admins only for other tenants), name, pageNumber, pageSize. Ownership filtering precedes pagination."));
            ctx.Server.Get(url+"/{id}", async req => Read(ctx,req,kind), api => Meta(api,kind,"Read a visible " + kind));
            ctx.Server.Post(url, async req =>
            {
                LibraryItem item=Body(ctx,req); return Save(ctx,req,kind,item,null);
            }, api => WriteMeta(api,kind,"Create a " + kind));
            ctx.Server.Put(url+"/{id}", async req =>
            {
                LibraryItem existing=Read(ctx,req,kind);
                RequireEdit(ctx,req,existing);
                return Save(ctx,req,kind,Body(ctx,req),existing);
            }, api => WriteMeta(api,kind,"Update a " + kind));
            ctx.Server.Post(url+"/{id}/duplicate", async req =>
            {
                LibraryItem source=Read(ctx,req,kind);
                LibraryItem target=Body(ctx,req);
                // Omitted payload fields inherit the visible source; edits apply only to the new copy.
                target.Content=target.Content ?? source.Content; target.Configuration=target.Configuration ?? source.Configuration;
                return Save(ctx,req,kind,target,null);
            }, api => WriteMeta(api,kind,"Duplicate a visible " + kind).WithDescription("Copies payload without changing the source. Scope defaults to User. Only platform administrators may publish System items, tenant administrators may publish Tenant items in their own tenant. User ownership is derived from the authenticated principal."));
            ctx.Server.Delete(url+"/{id}", async req =>
            {
                LibraryItem existing=Read(ctx,req,kind); RequireEdit(ctx,req,existing);
                ctx.Auth.AuthorizeLibrary(req,kind,OperationTypeEnum.Delete,existing.Id);
                ctx.Database.Library.Delete(existing.Id); Audit(ctx,req,existing,"Deleted");
                return new { Deleted=true };
            }, api => Meta(api,kind,"Delete a " + kind));
        }
        private static RequestContext Actor(ApiRequest req)
        {
            RequestContext actor=req.Http.Metadata as RequestContext;
            if(actor==null || actor.ShouldChallenge || !actor.IsAuthenticated)throw new WebserverException(ApiResultEnum.NotAuthorized,"Sign in to access the library.");
            return actor;
        }
        private static string Tenant(RouteContext ctx, ApiRequest req, RequestContext actor)
        {
            string requested=req.Http.Request.Query.Elements?["tenantId"];
            if(!actor.IsAdmin && !String.IsNullOrEmpty(requested) && requested!=actor.TenantGuid)ctx.Auth.DenyLibrary(req,"Cross-tenant library access is not permitted.");
            string id=requested ?? actor.TenantGuid ?? ctx.Database.Tenants.GetByName("default")?.Guid;
            if(!String.IsNullOrEmpty(id) && ctx.Database.Tenants.Read(id)?.Active!=true)throw new WebserverException(ApiResultEnum.BadRequest,"An active tenant is required.");
            return id;
        }
        private static LibraryItem Read(RouteContext ctx,ApiRequest req,string kind)
        {
            RequestContext actor=Actor(req);
            ctx.Auth.AuthorizeLibrary(req,kind,OperationTypeEnum.Read,RouteHelpers.RouteParam(req,"id"));
            LibraryItem item=ctx.Database.Library.Read(RouteHelpers.RouteParam(req,"id"));
            if(item?.Kind!=kind || !LibraryAccess.CanRead(item,actor,Tenant(ctx,req,actor)))throw new WebserverException(ApiResultEnum.NotFound,"Library item not found.");
            item.CanEdit=LibraryAccess.CanEdit(item,actor)&&ctx.Auth.CanUseLibrary(req,kind,OperationTypeEnum.Update,item.Id);
            item.CanDelete=LibraryAccess.CanEdit(item,actor)&&ctx.Auth.CanUseLibrary(req,kind,OperationTypeEnum.Delete,item.Id);return item;
        }
        private static void RequireEdit(RouteContext ctx,ApiRequest req,LibraryItem item)
        {
            if(!LibraryAccess.CanEdit(item,Actor(req)))ctx.Auth.DenyLibrary(req,"This shared item is read-only. Duplicate it into your own library to make changes.");
        }
        private static LibraryItem Body(RouteContext ctx,ApiRequest req)
        {
            try {return ctx.Serializer.DeserializeJson<LibraryItem>(req.Http.Request.DataAsString) ?? throw new WebserverException(ApiResultEnum.BadRequest,"A library item is required.");}
            catch(JsonException ex){throw new WebserverException(ApiResultEnum.BadRequest,"Invalid library payload: "+ex.Message);}
            catch(ArgumentException ex){throw new WebserverException(ApiResultEnum.BadRequest,"Invalid generation parameter: "+ex.Message);}
        }
        private static LibraryItem Save(RouteContext ctx,ApiRequest req,string kind,LibraryItem item,LibraryItem existing)
        {
            RequestContext actor=Actor(req);
            ctx.Auth.AuthorizeLibrary(req,kind,existing==null?OperationTypeEnum.Create:OperationTypeEnum.Update,existing?.Id);
            if(String.IsNullOrWhiteSpace(item.Name)||item.Name.Trim().Length>256)throw Bad("A name of 1 to 256 characters is required.");
            if((item.Description?.Length ?? 0)>4096)throw Bad("Description exceeds 4096 characters.");
            item.Name=item.Name.Trim(); item.Description=item.Description ?? "";item.Kind=kind;
            item.Id=existing?.Id ?? Guid.NewGuid().ToString("N");item.CreatedUtc=existing?.CreatedUtc ?? DateTime.UtcNow;item.UpdatedUtc=DateTime.UtcNow;
            if(item.Scope=="System") {item.TenantId=null;item.UserId=null;}
            else if(item.Scope=="Tenant") {item.TenantId=item.TenantId ?? Tenant(ctx,req,actor);item.UserId=null;}
            else if(item.Scope=="User")
            {
                if(String.IsNullOrEmpty(LibraryAccess.UserId(actor)))throw Bad("Sign in as a user to save personal items.");
                if(!String.IsNullOrEmpty(item.UserId)&&item.UserId!=LibraryAccess.UserId(actor))ctx.Auth.DenyLibrary(req,"User items must belong to your own account.");
                item.UserId=LibraryAccess.UserId(actor);item.TenantId=actor.TenantGuid;
            }
            else throw Bad("Scope must be System, Tenant, or User.");
            RequireEdit(ctx,req,item);
            if(existing!=null&&(existing.Scope!=item.Scope||existing.TenantId!=item.TenantId||existing.UserId!=item.UserId))throw Bad("Duplicate the item to change its visibility or owning tenant.");
            if(item.Scope!="System" && (String.IsNullOrEmpty(item.TenantId)||ctx.Database.Tenants.Read(item.TenantId)?.Active!=true))throw Bad("An active owning tenant is required.");
            if(kind=="SystemPrompt")
            {
                if(item.Configuration!=null)throw Bad("Store generation settings as a separate model configuration.");
                if(item.Content==null||item.Content.Length>1048576)throw Bad("Prompt content is required and must not exceed 1048576 characters.");
                item.Configuration=null;
            }
            else
            {
                if(item.Content!=null)throw Bad("Store system prompt content as a separate system prompt.");
                if(item.Configuration==null)throw Bad("A model configuration is required.");
                if(item.Configuration.Stop==null||item.Configuration.Stop.Count>64||item.Configuration.Stop.Any(value=>value==null||value.Length>2048))throw Bad("Use at most 64 stop sequences, each at most 2048 characters.");
                item.Content=null;
            }
            item.CanEdit=ctx.Auth.CanUseLibrary(req,kind,OperationTypeEnum.Update,item.Id);
            item.CanDelete=ctx.Auth.CanUseLibrary(req,kind,OperationTypeEnum.Delete,item.Id);
            if(existing==null)ctx.Database.Library.Create(item);else ctx.Database.Library.Update(item);
            Audit(ctx,req,item,existing==null?"Created":"Updated");return item;
        }
        private static void Audit(RouteContext ctx,ApiRequest req,LibraryItem item,string action)
        {
            RequestContext actor=Actor(req);
            ctx.Database.Audit.Create(new AuditLogEntry { TenantGuid=item.TenantId ?? actor.TenantGuid, EventType=item.Kind+action,PrincipalType=actor.PrincipalType,PrincipalGuid=actor.PrincipalGuid,Method=req.Http.Request.Method.ToString(),Path=req.Http.Request.Url.RawWithQuery,AuthResult=true,AuthzResult=true,StatusCode=200 });
        }
        private static WebserverException Bad(string message)=>new WebserverException(ApiResultEnum.BadRequest,message);
        private static OpenApiRouteMetadata Meta(OpenApiRouteMetadata api,string group,string summary)=>RouteHelpers.Describe(api,group,summary)
            .WithResponse(200,OpenApiResponseMetadata.Json(summary,OpenApiSchemaMetadata.Create("object")))
            .WithResponse(400,OpenApiResponseMetadata.BadRequest()).WithResponse(401,OpenApiResponseMetadata.Unauthorized()).WithResponse(403,OpenApiResponseMetadata.Json("Insufficient scope or explicit permission denial",OpenApiSchemaMetadata.Create("object"))).WithResponse(404,OpenApiResponseMetadata.NotFound());
        private static OpenApiRouteMetadata WriteMeta(OpenApiRouteMetadata api,string group,string summary)=>Meta(api,group,summary)
            .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"),"Name, Description, Scope, TenantId and Content or Configuration. IDs, timestamps and personal ownership are assigned by the server.",true));
    }
}
