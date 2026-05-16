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
}
