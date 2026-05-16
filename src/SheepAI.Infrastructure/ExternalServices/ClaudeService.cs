using System.Net.Http.Headers;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheepAI.Application.Interfaces.ExternalServices;
using SheepAI.Application.Options;
using SheepAI.Domain.Entities;
using BetaFiles = Anthropic.Models.Beta.Files;
using BetaMsg = Anthropic.Models.Beta.Messages;

namespace SheepAI.Infrastructure.ExternalServices;

public sealed class ClaudeService : IClaudeService
{
    private readonly AnthropicClient _client;
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeService> _logger;

    public ClaudeService(IOptions<ClaudeOptions> options, ILogger<ClaudeService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new AnthropicClient(new ClientOptions { ApiKey = _options.ApiKey });
    }

    public async Task<ClaudeResult> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Sending prompt to Claude (model: {Model})", _options.DefaultModel);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _options.DefaultModel,
            MaxTokens = _options.MaxTokens,
            Messages = [new MessageParam { Role = Role.User, Content = prompt }]
        }, cancellationToken);

        var text = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text ?? string.Empty;

        _logger.LogInformation("Claude responded ({InputTokens} in, {OutputTokens} out)",
            response.Usage.InputTokens, response.Usage.OutputTokens);

        return new ClaudeResult(
            text,
            (int)response.Usage.InputTokens,
            (int)response.Usage.OutputTokens);
    }

    public async Task<DocumentUploadResult> UploadDocumentAsync(Stream fileStream, string fileName, long sizeBytes, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Uploading document to Anthropic Files API: {FileName} ({SizeBytes} bytes)", fileName, sizeBytes);

        var fileContent = new BinaryContent
        {
            Stream = fileStream,
            FileName = fileName,
            ContentType = new MediaTypeHeaderValue("application/pdf")
        };
        var uploaded = await _client.Beta.Files.Upload(new BetaFiles.FileUploadParams { File = fileContent }, cancellationToken);

        _logger.LogInformation("Document uploaded to Files API: {FileId} ({FileName})", uploaded.ID, fileName);

        return new DocumentUploadResult(uploaded.ID, fileName, sizeBytes);
    }

    public async Task<ClaudeResult> AnalyzeDocumentAsync(string fileId, string prompt, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Analyzing document {FileId} with Claude (model: {Model})", fileId, _options.DefaultModel);

        BetaMsg.BetaRequestDocumentBlockSource docSource = new BetaMsg.BetaFileDocumentSource { FileID = fileId };
        BetaMsg.BetaContentBlockParam docContent = new BetaMsg.BetaRequestDocumentBlock { Source = docSource };
        BetaMsg.BetaContentBlockParam textContent = new BetaMsg.BetaTextBlockParam { Text = prompt };
        BetaMsg.BetaMessageParamContent msgContent = new List<BetaMsg.BetaContentBlockParam> { docContent, textContent };

        var response = await _client.Beta.Messages.Create(new BetaMsg.MessageCreateParams
        {
            Model = _options.DefaultModel,
            MaxTokens = _options.MaxTokens,
            Messages = [new BetaMsg.BetaMessageParam { Role = "user", Content = msgContent }],
            Betas = ["files-api-2025-04-14"]
        }, cancellationToken);

        var text = response.Content
            .Select(b => b.Value)
            .OfType<BetaMsg.BetaTextBlock>()
            .FirstOrDefault()?.Text ?? string.Empty;

        _logger.LogInformation("Document analysis complete ({InputTokens} in, {OutputTokens} out)",
            response.Usage.InputTokens, response.Usage.OutputTokens);

        return new ClaudeResult(
            text,
            (int)response.Usage.InputTokens,
            (int)response.Usage.OutputTokens);
    }

    public async Task DeleteDocumentAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Deleting document {FileId} from Anthropic Files API", fileId);

        await _client.Beta.Files.Delete(fileId, new BetaFiles.FileDeleteParams(), cancellationToken);

        _logger.LogInformation("Document deleted from Files API: {FileId}", fileId);
    }

    public async Task<ClaudeResult> ChatWithDocumentsAsync(
        IReadOnlyList<string> fileIds,
        IReadOnlyList<(string Role, string Content)> history,
        string userMessage,
        CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("ChatWithDocuments — {FileCount} docs, {HistoryCount} prior turns", fileIds.Count, history.Count);

        var messages = new List<BetaMsg.BetaMessageParam>();

        // Inject all documents as the first turn so they are available for the whole conversation.
        // Using cache_control ephemeral to enable prompt caching on the document blocks.
        if (fileIds.Count > 0)
        {
            var docBlocks = new List<BetaMsg.BetaContentBlockParam>();
            foreach (var id in fileIds)
            {
                BetaMsg.BetaRequestDocumentBlockSource src = new BetaMsg.BetaFileDocumentSource { FileID = id };
                docBlocks.Add(new BetaMsg.BetaRequestDocumentBlock { Source = src });
            }
            docBlocks.Add(new BetaMsg.BetaTextBlockParam
            {
                Text = "Ovo su gradski dokumenti Grada Splita koji su ti na raspolaganju za odgovaranje na pitanja građana."
            });

            messages.Add(new BetaMsg.BetaMessageParam
            {
                Role    = "user",
                Content = docBlocks
            });
            messages.Add(new BetaMsg.BetaMessageParam
            {
                Role    = "assistant",
                Content = new List<BetaMsg.BetaContentBlockParam>
                {
                    new BetaMsg.BetaTextBlockParam { Text = "Razumijem. Koristit ću ove gradske dokumente za odgovaranje na pitanja." }
                }
            });
        }

        // Append conversation history, normalizing roles for the Anthropic API.
        // Consecutive messages of the same role are merged to satisfy alternation requirement.
        foreach (var (role, content) in history)
        {
            var apiRole = role is "assistant" or "admin" ? "assistant" : "user";
            if (messages.Count > 0 && messages[^1].Role == apiRole)
            {
                // Merge into the previous message
                var prev    = messages[^1];
                var merged  = ((IEnumerable<BetaMsg.BetaContentBlockParam>)prev.Content!).ToList();
                merged.Add(new BetaMsg.BetaTextBlockParam { Text = content });
                messages[^1] = new BetaMsg.BetaMessageParam { Role = apiRole, Content = merged };
            }
            else
            {
                messages.Add(new BetaMsg.BetaMessageParam
                {
                    Role    = apiRole,
                    Content = new List<BetaMsg.BetaContentBlockParam>
                    {
                        new BetaMsg.BetaTextBlockParam { Text = content }
                    }
                });
            }
        }

        // Current user message
        messages.Add(new BetaMsg.BetaMessageParam
        {
            Role    = "user",
            Content = new List<BetaMsg.BetaContentBlockParam>
            {
                new BetaMsg.BetaTextBlockParam { Text = userMessage }
            }
        });

        const string systemPrompt =
            "Ti si AI asistent Grada Splita. Pomažeš građanima i turistima s pitanjima o gradskim uslugama, " +
            "administrativnim zahtjevima i informacijama o gradu. " +
            "Odgovaraj isključivo na temelju priloženih gradskih dokumenata. " +
            "Ako odgovor nije dostupan u dokumentima, ljubazno obavijesti korisnika da nemaš tu informaciju " +
            "i predloži da kontaktira gradske službe. " +
            "Uvijek odgovaraj na jeziku kojim je korisnik napisao svoju poruku — " +
            "ako piše na hrvatskom, odgovori na hrvatskom; ako piše na engleskom, odgovori na engleskom; itd. " +
            "Odgovaraj jasno i ljubazno.";

        var response = await _client.Beta.Messages.Create(new BetaMsg.MessageCreateParams
        {
            Model     = _options.DefaultModel,
            MaxTokens = _options.MaxTokens,
            System    = systemPrompt,
            Messages  = messages,
            Betas     = ["files-api-2025-04-14"]
        }, ct);

        var text = response.Content
            .Select(b => b.Value)
            .OfType<BetaMsg.BetaTextBlock>()
            .FirstOrDefault()?.Text ?? string.Empty;

        _logger.LogInformation("ChatWithDocuments complete ({InputTokens} in, {OutputTokens} out)",
            response.Usage.InputTokens, response.Usage.OutputTokens);

        return new ClaudeResult(
            text,
            (int)response.Usage.InputTokens,
            (int)response.Usage.OutputTokens);
    }
}
