using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class SkpdEndpoints
{
    public static IEndpointRouteBuilder MapSkpdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/skpd")
            .WithTags("SKPD")
            .RequireAuthorization();

        group.MapGet("/", async (ISkpdService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(cancellationToken)));

        group.MapGet("/{id:int}", async (int id, ISkpdService service, CancellationToken cancellationToken) =>
        {
            var item = await service.GetByIdAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost("/", async ([FromBody] CreateSkpdRequest request, ISkpdService service, CancellationToken cancellationToken) =>
        {
            var created = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/v1/skpd/{created.Id}", created);
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

        group.MapPut("/{id:int}", async (int id, [FromBody] UpdateSkpdRequest request, ISkpdService service, CancellationToken cancellationToken) =>
        {
            var updated = await service.UpdateAsync(id, request, cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

        group.MapDelete("/{id:int}", async (int id, ISkpdService service, CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

        return app;
    }
}
