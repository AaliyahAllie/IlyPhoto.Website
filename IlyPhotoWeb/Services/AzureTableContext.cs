using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace IlyPhotoWeb.Services;

/// <summary>
/// Manages connections to Azure Table Storage in ilyphotostorage.
/// Connects to the exact same cloud database tables used by the IlyPhoto Android app and Azure REST API.
/// </summary>
public class AzureTableContext
{
    private readonly TableServiceClient? _serviceClient;
    private readonly ILogger<AzureTableContext> _logger;
    private readonly Dictionary<string, TableClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public bool IsConnected => _serviceClient != null;

    public AzureTableContext(IOptions<AzureStorageOptions> options, ILogger<AzureTableContext> logger)
    {
        _logger = logger;
        var connStr = options.Value.ConnectionString;

        if (!string.IsNullOrWhiteSpace(connStr))
        {
            try
            {
                _serviceClient = new TableServiceClient(connStr);
                _logger.LogInformation("Successfully initialized Azure Table Storage client for ilyphotostorage.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Table Storage client. Falling back to local mode.");
            }
        }
        else
        {
            _logger.LogWarning("No AzureStorage:ConnectionString provided in appsettings.json.");
        }
    }

    public TableClient GetTable(string tableName)
    {
        lock (_clients)
        {
            if (_clients.TryGetValue(tableName, out var existing))
                return existing;

            if (_serviceClient != null)
            {
                var client = _serviceClient.GetTableClient(tableName);
                try
                {
                    client.CreateIfNotExists();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Table {TableName} CreateIfNotExists notice: {Message}", tableName, ex.Message);
                }
                _clients[tableName] = client;
                return client;
            }

            throw new InvalidOperationException($"Azure Table Storage is not configured for table '{tableName}'.");
        }
    }

    public TableClient Users => GetTable("users");
    public TableClient Bookings => GetTable("bookings");
    public TableClient Photos => GetTable("photos");
    public TableClient Enquiries => GetTable("enquiries");
    public TableClient Reviews => GetTable("reviews");
    public TableClient Invoices => GetTable("invoices");
    public TableClient Services => GetTable("services");
    public TableClient BlockedDates => GetTable("blockedDates");
    public TableClient Galleries => GetTable("galleries");
    public TableClient Counters => GetTable("counters");
}
