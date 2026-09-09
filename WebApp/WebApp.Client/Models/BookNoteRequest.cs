namespace WebApp.Client.Models;

public sealed record BookNoteRequest(
    string ChapterId,
    string SelectedText,
    int? TextOffsetStart,
    int? TextOffsetEnd);
