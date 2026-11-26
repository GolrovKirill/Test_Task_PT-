namespace PT.Api.Features.Documents;

public sealed class DocumentEventQuery
{
    public long? Since { get; set; }

    public int? Limit { get; set; }
}
