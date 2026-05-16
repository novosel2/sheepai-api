using System.Net.Http.Headers;
using System.Text.Json;
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

    // Forced tool used to get structured output (text + optional widgets) from every chat call.
    private static readonly BetaMsg.BetaTool RespondTool = BuildRespondTool();

    private static BetaMsg.BetaTool BuildRespondTool()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "text": {
                  "type": "string",
                  "description": "Your response to the user"
                },
                "widgets": {
                  "type": "array",
                  "description": "Optional visual widgets. Include a map widget when the answer involves a specific address or location with known coordinates from the documents.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "type": { "type": "string", "enum": ["map"] },
                      "config": {
                        "type": "object",
                        "properties": {
                          "lat": { "type": "number", "description": "Latitude" },
                          "lng": { "type": "number", "description": "Longitude" }
                        },
                        "required": ["lat", "lng"]
                      }
                    },
                    "required": ["type", "config"]
                  }
                }
              },
              "required": ["text"]
            }
            """;

        var rawData = JsonDocument.Parse(schemaJson).RootElement
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value);

        return new BetaMsg.BetaTool
        {
            Name        = "respond",
            Description = "Respond to the user. Always include a text response. Add a map widget only when the answer involves a specific location with known coordinates from the city documents.",
            InputSchema = BetaMsg.InputSchema.FromRawUnchecked(rawData)
        };
    }

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

    public async Task<DocumentUploadResult> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Uploading document to Anthropic Files API: {FileName} ({SizeBytes} bytes, {ContentType})", fileName, sizeBytes, contentType);

        var fileContent = new BinaryContent
        {
            Stream = fileStream,
            FileName = fileName,
            ContentType = new MediaTypeHeaderValue(contentType)
        };
        var uploaded = await _client.Beta.Files.Upload(new BetaFiles.FileUploadParams { File = fileContent }, cancellationToken);

        _logger.LogInformation("Document uploaded to Files API: {FileId} ({FileName})", uploaded.ID, fileName);

        return new DocumentUploadResult(uploaded.ID, fileName, sizeBytes);
    }

    public async Task DeleteDocumentAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Deleting document {FileId} from Anthropic Files API", fileId);

        await _client.Beta.Files.Delete(fileId, new BetaFiles.FileDeleteParams(), cancellationToken);

        _logger.LogInformation("Document deleted from Files API: {FileId}", fileId);
    }

    public async Task<string> GenerateChatNameAsync(string firstMessage, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Generating chat name for first message");

        var prompt =
            "Na temelju poruke građanina, osmisli kratki naziv razgovora (najviše 6 riječi). " +
            "Naziv treba biti jasan i opisivati konkretnu temu ili zahtjev.\n\n" +
            "Ako je poruka pozdrav, uvreda, besmislica ili previše neodređena za smisleni naziv, " +
            "odgovori SAMO riječju \"SKIP\" i ničim drugim.\n\n" +
            "Inače odgovori SAMO nazivom, bez navodnika ili ikakvih dodatnih objašnjenja.\n\n" +
            $"Poruka: {firstMessage}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = _options.DefaultModel,
            MaxTokens = 20,
            Messages  = [new MessageParam { Role = Role.User, Content = prompt }]
        }, ct);

        var name = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text?.Trim() ?? string.Empty;

        if (name.Equals("SKIP", StringComparison.OrdinalIgnoreCase))
            name = string.Empty;

        _logger.LogInformation("Generated chat name: {Name}", string.IsNullOrEmpty(name) ? "(skipped)" : name);
        return name;
    }

    public async Task<string> GenerateChatSummaryAsync(IReadOnlyList<(string Role, string Content)> history, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Generating chat summary for conversation with {TurnCount} turns", history.Count);

        var transcript = string.Join("\n", history.Select(h =>
            $"{(h.Role == "user" ? "Građanin" : "Asistent")}: {h.Content}"));

        var prompt =
            "Na temelju sljedećeg razgovora, napiši 1-2 rečenice koje opisuju što građanin traži ili želi. " +
            "Fokusiraj se isključivo na zahtjev ili problem građanina — ne na to je li AI pronašao odgovor.\n\n" +
            "Ako razgovor ne sadrži nikakav smislen zahtjev (npr. samo pozdravi, uvrede ili besmislice), " +
            "odgovori SAMO riječju \"SKIP\" i ničim drugim.\n\n" +
            "Inače odgovori SAMO opisom zahtjeva, bez uvoda ili dodatnih komentara.\n\n" +
            $"Razgovor:\n{transcript}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = _options.DefaultModel,
            MaxTokens = 100,
            Messages  = [new MessageParam { Role = Role.User, Content = prompt }]
        }, ct);

        var summary = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text?.Trim() ?? string.Empty;

        if (summary.Equals("SKIP", StringComparison.OrdinalIgnoreCase))
            summary = string.Empty;

        _logger.LogInformation("Generated chat summary ({Length} chars)", summary.Length);
        return summary;
    }

    public async Task<bool> CheckUrgencyAsync(
        IReadOnlyList<(string Role, string Content)> history,
        CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Checking urgency for conversation with {TurnCount} turns", history.Count);

        var transcript = string.Join("\n", history.Select(h =>
            $"{(h.Role == "user" ? "Građanin" : "Asistent")}: {h.Content}"));

        var prompt =
            "Procijeni treba li sljedeći razgovor između građanina i gradskog AI asistenta hitnu pažnju ljudskog administratora.\n\n" +
            $"Razgovor:\n{transcript}\n\n" +
            "Odgovori SAMO riječju \"urgent\" ili \"not_urgent\". " +
            "Označi kao hitno ako BILO KOJI od ovih uvjeta vrijedi:\n" +
            "- Osoba ima vremenski osjetljiv problem, u je nevolji ili treba neposrednu pomoć čovjeka.\n" +
            "- AI asistent je izjavio da nema informaciju ili ne može odgovoriti na pitanje građanina.";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = _options.DefaultModel,
            MaxTokens = 10,
            Messages  = [new MessageParam { Role = Role.User, Content = prompt }]
        }, ct);

        var answer = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text ?? string.Empty;

        var isUrgent = answer.Contains("urgent", StringComparison.OrdinalIgnoreCase)
                    && !answer.Contains("not_urgent", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("Urgency check result: {Result}", isUrgent ? "urgent" : "not_urgent");
        return isUrgent;
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
        // Cache breakpoint 2 sits on the trailing text block — everything above it (all doc refs)
        // is served from cache on subsequent requests as long as the document list doesn't change.
        if (fileIds.Count > 0)
        {
            var docBlocks = new List<BetaMsg.BetaContentBlockParam>();
            foreach (var id in fileIds)
            {
                BetaMsg.BetaRequestDocumentBlockSource src = new BetaMsg.BetaFileDocumentSource { FileID = id };
                docBlocks.Add(new BetaMsg.BetaRequestDocumentBlock { Source = src });
            }
            // Cache breakpoint 2 — placed here so all document blocks above are cached together.
            docBlocks.Add(new BetaMsg.BetaTextBlockParam
            {
                Text = "Ovo su gradski dokumenti Grada Splita koji su ti na raspolaganju za odgovaranje na pitanja građana.",
                CacheControl = new BetaMsg.BetaCacheControlEphemeral()
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
        // Consecutive same-role messages are merged by accumulating blocks before committing
        // the BetaMessageParam — avoids extracting content from the SDK's union type.
        string? pendingRole = null;
        var pendingBlocks = new List<BetaMsg.BetaContentBlockParam>();

        void FlushPending()
        {
            if (pendingRole is null) return;
            messages.Add(new BetaMsg.BetaMessageParam
            {
                Role    = pendingRole,
                Content = new List<BetaMsg.BetaContentBlockParam>(pendingBlocks)
            });
            pendingRole = null;
            pendingBlocks.Clear();
        }

        foreach (var (role, content) in history)
        {
            var apiRole = role is "assistant" or "admin" ? "assistant" : "user";
            if (pendingRole != apiRole) FlushPending();
            pendingRole = apiRole;
            pendingBlocks.Add(new BetaMsg.BetaTextBlockParam { Text = content });
        }
        FlushPending();

        // Current user message
        messages.Add(new BetaMsg.BetaMessageParam
        {
            Role    = "user",
            Content = new List<BetaMsg.BetaContentBlockParam>
            {
                new BetaMsg.BetaTextBlockParam { Text = userMessage }
            }
        });

        // Cache breakpoint 1 on the system prompt — it never changes, so it is always served
        // from cache after the very first request (charged at 10% of normal input token cost).
        const string systemPromptText =
            "Ti si AI asistent Grada Splita. Pomažeš građanima i turistima s pitanjima o gradskim uslugama, " +
            "administrativnim zahtjevima i informacijama o gradu. " +
            "Odgovaraj isključivo na temelju priloženih gradskih dokumenata. " +
            "Ako odgovor nije dostupan u dokumentima, odgovori točno ovom rečenicom (prevedenom na jezik korisnika ako je potrebno): " +
            "\"Nažalost, nemam dovoljno informacija u dostupnim dokumentima da bih mogao odgovoriti na vaše pitanje. " +
            "Proslijedit ću vaš upit administratoru koji će vam se javiti u najkraćem mogućem roku.\" " +
            "Nemoj dodavati ništa drugo uz tu rečenicu. " +
            "Uvijek odgovaraj na jeziku kojim je korisnik napisao svoju poruku — " +
            "ako piše na engleskom, odgovori na engleskom; itd. " +
            "Ako korisnik piše nekim jezikom koji je sličan ili blizak hrvatskom, uvijek odgovaraj na hrvatskom. " +
            "Odgovaraj jasno i ljubazno. " +
            "Uvijek odgovaraj pomoću alata 'respond'. " +
            "U polje 'widgets' dodaj map widget jedino ako odgovor uključuje konkretnu lokaciju ili adresu čije su koordinate poznate iz dokumenata.";

        List<BetaMsg.BetaTextBlockParam> systemBlocks =
        [
            new() { Text = systemPromptText, CacheControl = new BetaMsg.BetaCacheControlEphemeral() }
        ];

        var response = await _client.Beta.Messages.Create(new BetaMsg.MessageCreateParams
        {
            Model      = _options.DefaultModel,
            MaxTokens  = _options.MaxTokens,
            System     = systemBlocks,
            Messages   = messages,
            Tools      = [new BetaMsg.BetaToolUnion(RespondTool, null)],
            ToolChoice = new BetaMsg.BetaToolChoice(new BetaMsg.BetaToolChoiceTool { Name = "respond" }, null),
            Betas      = ["files-api-2025-04-14"]
        }, ct);

        var toolUse = response.Content
            .Select(b => b.Value)
            .OfType<BetaMsg.BetaToolUseBlock>()
            .FirstOrDefault();

        var text = string.Empty;
        string? widgetsJson = null;

        if (toolUse is not null)
        {
            if (toolUse.Input.TryGetValue("text", out var textEl))
                text = textEl.GetString() ?? string.Empty;

            if (toolUse.Input.TryGetValue("widgets", out var widgetsEl)
                && widgetsEl.ValueKind == JsonValueKind.Array
                && widgetsEl.GetArrayLength() > 0)
            {
                widgetsJson = widgetsEl.GetRawText();
            }
        }

        _logger.LogInformation("ChatWithDocuments complete ({InputTokens} in, {OutputTokens} out)",
            response.Usage.InputTokens, response.Usage.OutputTokens);

        return new ClaudeResult(
            text,
            (int)response.Usage.InputTokens,
            (int)response.Usage.OutputTokens,
            widgetsJson);
    }
}
