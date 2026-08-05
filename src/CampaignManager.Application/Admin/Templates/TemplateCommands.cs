using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Application.Templating;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Admin.Templates;

public sealed record TemplateSummary(
    Guid Id, string Name, string Channel, string? Subject, string Body, bool IsActive,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed class SaveTemplateInput
{
    public Guid? Id { get; init; }
    public required string Name { get; init; }
    public required string Channel { get; init; }
    public string? Subject { get; init; }
    public required string Body { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record SaveTemplateCommand(SaveTemplateInput Input) : IRequest<Guid>;

public sealed class SaveTemplateHandler : IRequestHandler<SaveTemplateCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public SaveTemplateHandler(IAppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(SaveTemplateCommand command, CancellationToken ct)
    {
        var organizationId = _tenant.OrganizationId ?? throw new DomainException("No organization context.");
        var input = command.Input;
        if (!Enum.TryParse<Channel>(input.Channel, ignoreCase: true, out var channel))
        {
            throw new DomainException("Channel must be one of: Sms, Email, WhatsApp.");
        }

        MessageTemplate template;
        if (input.Id is { } id)
        {
            template = await _db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new NotFoundException(nameof(MessageTemplate), id);
            template.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            template = new MessageTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = input.Name,
                Channel = channel,
                Body = input.Body,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.MessageTemplates.Add(template);
        }

        template.Name = input.Name;
        template.Channel = channel;
        template.Subject = input.Subject;
        template.Body = input.Body;
        template.IsActive = input.IsActive;
        await _db.SaveChangesAsync(ct);
        return template.Id;
    }
}

public sealed record DeleteTemplateCommand(Guid TemplateId) : IRequest;

public sealed class DeleteTemplateHandler : IRequestHandler<DeleteTemplateCommand>
{
    private readonly IAppDbContext _db;

    public DeleteTemplateHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DeleteTemplateCommand command, CancellationToken ct)
    {
        var template = await _db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct)
            ?? throw new NotFoundException(nameof(MessageTemplate), command.TemplateId);
        var used = await _db.Campaigns.IgnoreQueryFilters().AnyAsync(c => c.TemplateId == template.Id, ct);
        if (used)
        {
            template.IsActive = false;  // keep referential history; hide from new campaigns
        }
        else
        {
            _db.MessageTemplates.Remove(template);
        }

        await _db.SaveChangesAsync(ct);
    }
}

public sealed record ListTemplatesQuery : IRequest<IReadOnlyList<TemplateSummary>>;

public sealed class ListTemplatesHandler : IRequestHandler<ListTemplatesQuery, IReadOnlyList<TemplateSummary>>
{
    private readonly IAppDbContext _db;

    public ListTemplatesHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TemplateSummary>> Handle(ListTemplatesQuery query, CancellationToken ct) =>
        await _db.MessageTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TemplateSummary(
                t.Id, t.Name, t.Channel.ToString(), t.Subject, t.Body, t.IsActive,
                t.CreatedAtUtc, t.UpdatedAtUtc))
            .ToListAsync(ct);
}

public sealed record PreviewTemplateQuery(
    Guid? TemplateId, string? Body, string? Subject,
    Dictionary<string, string> SampleData) : IRequest<TemplatePreview>;

public sealed record TemplatePreview(string? Subject, string Body);

public sealed class PreviewTemplateHandler : IRequestHandler<PreviewTemplateQuery, TemplatePreview>
{
    private readonly IAppDbContext _db;
    private readonly ITemplateRenderer _renderer;

    public PreviewTemplateHandler(IAppDbContext db, ITemplateRenderer renderer)
    {
        _db = db;
        _renderer = renderer;
    }

    public async Task<TemplatePreview> Handle(PreviewTemplateQuery query, CancellationToken ct)
    {
        var body = query.Body;
        var subject = query.Subject;
        if (query.TemplateId is { } id)
        {
            var template = await _db.MessageTemplates.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new NotFoundException(nameof(MessageTemplate), id);
            body ??= template.Body;
            subject ??= template.Subject;
        }

        return new TemplatePreview(
            subject is null ? null : _renderer.Render(subject, query.SampleData),
            _renderer.Render(body ?? string.Empty, query.SampleData));
    }
}
