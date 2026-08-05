using CampaignManager.Contracts.Campaigns;
using MediatR;

namespace CampaignManager.Application.Campaigns.Commands.CreateCampaign;

public sealed record CreateCampaignCommand(CreateCampaignRequest Request)
    : IRequest<CreateCampaignResponse>;
