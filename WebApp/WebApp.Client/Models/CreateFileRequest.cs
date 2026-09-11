namespace WebApp.Client.Models;

public sealed record CreateFileRequest(string? ParentId, string Name, string Extension);
