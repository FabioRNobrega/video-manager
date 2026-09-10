namespace WebApp.Client.Models;

public sealed record BookProgressDto(
    string ChapterId,
    int WordOffset);
