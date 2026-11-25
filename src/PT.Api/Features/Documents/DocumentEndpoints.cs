using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PT.Api.Common;
using PT.Domain.Entities;
using PT.Infrastructure.Data;

namespace PT.Api.Features.Documents;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documents").WithTags("Documents");

        group.MapPost(string.Empty, CreateAsync)
            .WithSummary("Создать документ")
            .WithDescription("Создаёт документ. Id и даты CreatedAt/UpdatedAt назначает сервер.")
            .Produces<Document>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<DocumentWriteRequest>>();

        group.MapGet(string.Empty, ListAsync)
            .WithSummary("Получить список документов")
            .WithDescription("Offset-пагинация: page от 1, pageSize от 1 до 100. Порядок: createdAt desc, id desc.")
            .Produces<PagedResult<Document>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AddEndpointFilter<ValidationFilter<DocumentListQuery>>();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithSummary("Получить документ по Id")
            .Produces<Document>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithSummary("Полностью обновить документ")
            .WithDescription("Заменяет 17 клиентских полей. Id и CreatedAt не меняются, UpdatedAt обновляется.")
            .Produces<Document>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<DocumentWriteRequest>>();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithSummary("Удалить документ")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        DocumentWriteRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var document = new Document
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        Apply(document, request);

        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/documents/{document.Id}", document);
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] DocumentListQuery query,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? 20;

        var totalItems = await db.Documents.CountAsync(cancellationToken);
        var items = await db.Documents
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        return Results.Ok(new PagedResult<Document>(items, page, pageSize, totalItems, totalPages));
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return document is null ? NotFound(id) : Results.Ok(document);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        DocumentWriteRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound(id);
        }

        Apply(document, request);
        document.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(document);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound(id);
        }

        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static void Apply(Document document, DocumentWriteRequest request)
    {
        document.Title = request.Title!.Trim();
        document.Description = request.Description;
        document.Content = request.Content!;
        document.DocumentType = request.DocumentType!.Value;
        document.Status = request.Status!.Value;
        document.Confidentiality = request.Confidentiality!.Value;
        document.IsPublic = request.IsPublic;
        document.Priority = request.Priority!.Value;
        document.QualityScore = request.QualityScore!.Value;
        document.SizeBytes = request.SizeBytes!.Value;
        document.Tags = request.Tags;
        document.RelatedDocumentIds = request.RelatedDocumentIds;
        document.OwnerEmail = request.OwnerEmail!;
        document.EffectiveFrom = request.EffectiveFrom!.Value;
        document.ExpiresAt = request.ExpiresAt;
        document.ArchiveDate = request.ArchiveDate;
        document.Metadata = request.Metadata;
    }

    private static IResult NotFound(Guid id) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Документ не найден",
            detail: $"Документ с id '{id}' не существует.");
}
