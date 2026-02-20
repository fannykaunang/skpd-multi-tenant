namespace skpd_multi_tenant_api.Models;

// ─── Flat entity (mapped directly from a DB row) ───────────────────────────

public sealed class MenuItemFlat
{
    public int    Id        { get; set; }
    public int    MenuId    { get; set; }
    public int?   ParentId  { get; set; }
    public string Title     { get; set; } = string.Empty;
    public string Url       { get; set; } = string.Empty;
    public int    SortOrder { get; set; }
    public bool   IsActive  { get; set; }
}

// ─── Tree node (used in nested response) ──────────────────────────────────

public sealed class MenuItemNode
{
    public int              Id        { get; set; }
    public int?             ParentId  { get; set; }
    public string           Title     { get; set; } = string.Empty;
    public string           Url       { get; set; } = string.Empty;
    public int              SortOrder { get; set; }
    public bool             IsActive  { get; set; }
    public List<MenuItemNode> Children  { get; set; } = [];
}

// ─── Menu list summary (lightweight, for index view) ──────────────────────

public sealed class MenuSummary
{
    public int      Id        { get; set; }
    public int      SkpdId    { get; set; }
    public string   SkpdNama  { get; set; } = string.Empty;
    public string   Name      { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int      ItemCount { get; set; }
}

// ─── Menu response (header + nested tree) ─────────────────────────────────

public sealed class MenuResponse
{
    public int               Id        { get; set; }
    public int               SkpdId    { get; set; }
    public string            Name      { get; set; } = string.Empty;
    public DateTime          CreatedAt { get; set; }
    public List<MenuItemNode> Items     { get; set; } = [];
}

// ─── Request DTOs ──────────────────────────────────────────────────────────

public sealed class CreateMenuRequest
{
    /// <summary>Target SKPD — required when caller is SuperAdmin.</summary>
    public int?   SkpdId { get; set; }
    public string Name   { get; set; } = string.Empty;
}

public sealed class UpdateMenuRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateMenuItemRequest
{
    public int?   ParentId  { get; set; }
    public string Title     { get; set; } = string.Empty;
    public string Url       { get; set; } = string.Empty;
    public int    SortOrder { get; set; } = 0;
    public bool   IsActive  { get; set; } = true;
}

public sealed class UpdateMenuItemRequest
{
    public int?   ParentId  { get; set; }
    public string Title     { get; set; } = string.Empty;
    public string Url       { get; set; } = string.Empty;
    public int    SortOrder { get; set; } = 0;
    public bool   IsActive  { get; set; } = true;
}

/// <summary>Single entry in a drag-and-drop reorder request.</summary>
public sealed class ReorderItem
{
    public int  Id        { get; set; }
    public int? ParentId  { get; set; }
    public int  SortOrder { get; set; }
}
