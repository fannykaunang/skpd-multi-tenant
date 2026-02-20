using System.Security.Claims;
using skpd_multi_tenant_api.Extensions;
using skpd_multi_tenant_api.Models;
using skpd_multi_tenant_api.Services;

namespace skpd_multi_tenant_api.Endpoints;

public static class MenuEndpoints
{
    public static IEndpointRouteBuilder MapMenuEndpoints(this IEndpointRouteBuilder app)
    {
        // ══════════════════════════════════════════════════════════════════════
        // MENU  — /api/v1/menus
        // ══════════════════════════════════════════════════════════════════════

        var menuGroup = app.MapGroup("/api/v1/menus")
            .WithTags("Menus")
            .RequireAuthorization();

        // ── GET /api/v1/menus ──────────────────────────────────────────────
        // SuperAdmin: semua menu. Operator: hanya milik SKPD-nya.
        menuGroup.MapGet("/", async (
            ClaimsPrincipal  user,
            IMenuService     service,
            CancellationToken ct) =>
        {
            var skpdId = user.IsSuperAdmin() ? null : user.GetSkpdId();
            var menus  = await service.GetAllAsync(skpdId, ct);
            return Results.Ok(new { status = "success", data = menus });
        }).RequireAuthorization("CanViewMenu");

        // ── GET /api/v1/menus/{id} ─────────────────────────────────────────
        // Returns the menu header with a fully nested item tree.
        // Only one DB query is issued for menu_items; tree is built in C#.
        menuGroup.MapGet("/{id:int}", async (
            int              id,
            ClaimsPrincipal  user,
            IMenuService     service,
            CancellationToken ct) =>
        {
            var menu = await service.GetByIdAsync(id, ct);
            if (menu is null)
                return Results.NotFound(new { status = "error", message = "Menu tidak ditemukan." });

            // Tenant guard: non-superadmin can only read their own SKPD's menus
            if (!user.IsSuperAdmin())
            {
                var userSkpdId = user.GetSkpdId();
                if (menu.SkpdId != userSkpdId)
                    return Results.Forbid();
            }

            return Results.Ok(new { status = "success", data = menu });
        }).RequireAuthorization("CanViewMenu");

        // ── POST /api/v1/menus ─────────────────────────────────────────────
        menuGroup.MapPost("/", async (
            CreateMenuRequest request,
            ClaimsPrincipal   user,
            IMenuService      service,
            IAuditService     auditService,
            HttpContext       httpContext,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { status = "error", message = "Nama menu wajib diisi." });

            // Resolve target SKPD
            int skpdId;
            if (user.IsSuperAdmin())
            {
                if (!request.SkpdId.HasValue || request.SkpdId <= 0)
                    return Results.BadRequest(new
                    {
                        status  = "error",
                        message = "SuperAdmin harus menentukan skpdId pada request body."
                    });
                skpdId = request.SkpdId.Value;
            }
            else
            {
                var userSkpdId = user.GetSkpdId();
                if (!userSkpdId.HasValue)
                    return Results.BadRequest(new { status = "error", message = "SKPD tidak ditemukan pada sesi login." });
                skpdId = userSkpdId.Value;
            }

            var created = await service.CreateMenuAsync(skpdId, request, ct);

            await auditService.LogAsync(user, httpContext,
                "CREATE_MENU", "menu.create", "menus", created.Id,
                "success", "created", oldData: null, newData: created, ct);

            return Results.Created($"/api/v1/menus/{created.Id}",
                new { status = "success", data = created });
        }).RequireAuthorization("CanCreateMenu");

        // ── PUT /api/v1/menus/{id} ─────────────────────────────────────────
        menuGroup.MapPut("/{id:int}", async (
            int               id,
            UpdateMenuRequest request,
            ClaimsPrincipal   user,
            IMenuService      service,
            IAuditService     auditService,
            HttpContext       httpContext,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { status = "error", message = "Nama menu wajib diisi." });

            // Ownership check
            var menuSkpdId = await service.GetMenuSkpdIdAsync(id, ct);
            if (menuSkpdId is null)
                return Results.NotFound(new { status = "error", message = "Menu tidak ditemukan." });

            if (!user.IsSuperAdmin())
            {
                if (menuSkpdId != user.GetSkpdId())
                    return Results.Forbid();
            }

            var updated = await service.UpdateMenuAsync(id, request, ct);
            if (!updated)
                return Results.NotFound(new { status = "error", message = "Menu tidak ditemukan." });

            await auditService.LogAsync(user, httpContext,
                "UPDATE_MENU", "menu.update", "menus", id,
                "success", "updated", oldData: null, newData: request, ct);

            return Results.Ok(new { status = "success", message = "Menu berhasil diperbarui." });
        }).RequireAuthorization("CanEditMenu");

        // ── DELETE /api/v1/menus/{id} ──────────────────────────────────────
        // FK ON DELETE CASCADE will remove all menu_items automatically.
        menuGroup.MapDelete("/{id:int}", async (
            int               id,
            ClaimsPrincipal   user,
            IMenuService      service,
            IAuditService     auditService,
            HttpContext       httpContext,
            CancellationToken ct) =>
        {
            var menuSkpdId = await service.GetMenuSkpdIdAsync(id, ct);
            if (menuSkpdId is null)
                return Results.NotFound(new { status = "error", message = "Menu tidak ditemukan." });

            if (!user.IsSuperAdmin())
            {
                if (menuSkpdId != user.GetSkpdId())
                    return Results.Forbid();
            }

            var deleted = await service.DeleteMenuAsync(id, ct);
            if (!deleted)
                return Results.NotFound(new { status = "error", message = "Menu tidak ditemukan." });

            await auditService.LogAsync(user, httpContext,
                "DELETE_MENU", "menu.delete", "menus", id,
                "success", "deleted", oldData: null, newData: null, ct);

            return Results.Ok(new { status = "success", message = "Menu dan semua item berhasil dihapus." });
        }).RequireAuthorization("CanDeleteMenu");

        // ── PUT /api/v1/menus/{menuId}/reorder ────────────────────────────
        // Drag-and-drop bulk reorder.
        // All validation (ownership, cycles, depth, IDs belong to menu) happens
        // inside the service before the transaction is opened.
        menuGroup.MapPut("/{menuId:int}/reorder", async (
            int                       menuId,
            IReadOnlyList<ReorderItem> items,
            ClaimsPrincipal           user,
            IMenuService              service,
            IAuditService             auditService,
            HttpContext               httpContext,
            CancellationToken         ct) =>
        {
            var menuSkpdId = await service.GetMenuSkpdIdAsync(menuId, ct);
            if (menuSkpdId is null)
                return Results.NotFound(new { status = "error", message = "Menu tidak ditemukan." });

            if (!user.IsSuperAdmin())
            {
                if (menuSkpdId != user.GetSkpdId())
                    return Results.Forbid();
            }

            try
            {
                var updated = await service.ReorderItemsAsync(menuId, items, ct);

                await auditService.LogAsync(user, httpContext,
                    "REORDER_MENU", "menu.reorder", "menu_items", menuId,
                    "success", "reordered",
                    oldData: null,
                    newData: new { menuId, itemCount = items.Count },
                    ct);

                return Results.Ok(new { status = "success", data = updated });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { status = "error", message = ex.Message });
            }
        }).RequireAuthorization("CanEditMenu");

        // ══════════════════════════════════════════════════════════════════════
        // MENU ITEMS  — /api/v1/menus/{menuId}/items  &  /api/v1/menu-items/{id}
        // ══════════════════════════════════════════════════════════════════════

        var itemGroup = app.MapGroup("/api/v1")
            .WithTags("Menus")
            .RequireAuthorization();

        // ── POST /api/v1/menus/{menuId}/items ─────────────────────────────
        // Validations (inside service): parent in same menu, depth ≤ 5
        itemGroup.MapPost("/menus/{menuId:int}/items", async (
            int                    menuId,
            CreateMenuItemRequest  request,
            ClaimsPrincipal        user,
            IMenuService           service,
            IAuditService          auditService,
            HttpContext            httpContext,
            CancellationToken      ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return Results.BadRequest(new { status = "error", message = "Title menu item wajib diisi." });

            if (string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { status = "error", message = "URL menu item wajib diisi." });

            // Tenant check
            var menuSkpdId = await service.GetMenuSkpdIdAsync(menuId, ct);
            if (menuSkpdId is null)
                return Results.NotFound(new { status = "error", message = "Menu tidak ditemukan." });

            if (!user.IsSuperAdmin())
            {
                if (menuSkpdId != user.GetSkpdId())
                    return Results.Forbid();
            }

            try
            {
                var created = await service.CreateMenuItemAsync(menuId, request, ct);

                await auditService.LogAsync(user, httpContext,
                    "CREATE_MENU_ITEM", "menu.item.create", "menu_items", created.Id,
                    "success", "created", oldData: null, newData: created, ct);

                return Results.Created($"/api/v1/menus/{menuId}/items/{created.Id}",
                    new { status = "success", data = created });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { status = "error", message = ex.Message });
            }
        }).RequireAuthorization("CanCreateMenu");

        // ── PUT /api/v1/menu-items/{id} ────────────────────────────────────
        // Validations: same parent menu, no self-parent, no cycle, depth ≤ 5
        itemGroup.MapPut("/menu-items/{id:int}", async (
            int                   id,
            UpdateMenuItemRequest request,
            ClaimsPrincipal       user,
            IMenuService          service,
            IAuditService         auditService,
            HttpContext           httpContext,
            CancellationToken     ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return Results.BadRequest(new { status = "error", message = "Title menu item wajib diisi." });

            if (string.IsNullOrWhiteSpace(request.Url))
                return Results.BadRequest(new { status = "error", message = "URL menu item wajib diisi." });

            // Fetch item to get its menu, then check tenant ownership
            var item = await service.GetMenuItemAsync(id, ct);
            if (item is null)
                return Results.NotFound(new { status = "error", message = "Menu item tidak ditemukan." });

            var menuSkpdId = await service.GetMenuSkpdIdAsync(item.MenuId, ct);
            if (!user.IsSuperAdmin())
            {
                if (menuSkpdId != user.GetSkpdId())
                    return Results.Forbid();
            }

            try
            {
                var updated = await service.UpdateMenuItemAsync(id, request, ct);
                if (!updated)
                    return Results.NotFound(new { status = "error", message = "Menu item tidak ditemukan." });

                await auditService.LogAsync(user, httpContext,
                    "UPDATE_MENU_ITEM", "menu.item.update", "menu_items", id,
                    "success", "updated", oldData: item, newData: request, ct);

                return Results.Ok(new { status = "success", message = "Menu item berhasil diperbarui." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { status = "error", message = ex.Message });
            }
        }).RequireAuthorization("CanEditMenu");

        // ── DELETE /api/v1/menu-items/{id} ────────────────────────────────
        // FK ON DELETE CASCADE removes all children of this item.
        itemGroup.MapDelete("/menu-items/{id:int}", async (
            int               id,
            ClaimsPrincipal   user,
            IMenuService      service,
            IAuditService     auditService,
            HttpContext       httpContext,
            CancellationToken ct) =>
        {
            var item = await service.GetMenuItemAsync(id, ct);
            if (item is null)
                return Results.NotFound(new { status = "error", message = "Menu item tidak ditemukan." });

            var menuSkpdId = await service.GetMenuSkpdIdAsync(item.MenuId, ct);
            if (!user.IsSuperAdmin())
            {
                if (menuSkpdId != user.GetSkpdId())
                    return Results.Forbid();
            }

            var deleted = await service.DeleteMenuItemAsync(id, ct);
            if (!deleted)
                return Results.NotFound(new { status = "error", message = "Menu item tidak ditemukan." });

            await auditService.LogAsync(user, httpContext,
                "DELETE_MENU_ITEM", "menu.item.delete", "menu_items", id,
                "success", "deleted", oldData: item, newData: null, ct);

            return Results.Ok(new
            {
                status  = "success",
                message = "Menu item dan semua child berhasil dihapus."
            });
        }).RequireAuthorization("CanDeleteMenu");

        return app;
    }
}
