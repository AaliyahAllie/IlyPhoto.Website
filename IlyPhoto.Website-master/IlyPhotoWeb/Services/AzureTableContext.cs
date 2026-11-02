using System.Collections.Concurrent;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace IlyPhotoWeb.Services;

/// <summary>
/// Manages connections to Azure Table Storage in ilyphotostorage.
/// Connects to the exact same cloud database tables used by the IlyPhoto Android app and Azure REST API,
/// with deterministic in-memory table support when running automated tests or in local fallback mode.
/// </summary>
public class AzureTableContext
{
    private readonly TableServiceClient? _serviceClient;
    private readonly ILogger<AzureTableContext> _logger;
    private readonly Dictionary<string, TableClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TableEntity>> _memoryTables =
        new(StringComparer.OrdinalIgnoreCase);

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
                _logger.LogError(ex, "Failed to initialize Azure Table Storage client. Falling back to local in-memory mode.");
            }
        }
        else
        {
            _logger.LogWarning("No AzureStorage:ConnectionString provided in appsettings.json. Using in-memory table store.");
        }
    }

    public TableClient? TryGetTable(string tableName)
    {
        if (_serviceClient == null) return null;

        lock (_clients)
        {
            if (_clients.TryGetValue(tableName, out var existing))
                return existing;

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
    }

    public TableClient GetTable(string tableName) =>
        TryGetTable(tableName) ?? throw new InvalidOperationException($"Azure Table Storage is not configured for table '{tableName}'.");

    private ConcurrentDictionary<string, TableEntity> GetMemoryTable(string tableName) =>
        _memoryTables.GetOrAdd(tableName, _ => new ConcurrentDictionary<string, TableEntity>(StringComparer.OrdinalIgnoreCase));

    private static string MakeKey(string partitionKey, string rowKey) => $"{partitionKey}::{rowKey}";

    public async Task<TableEntity?> GetEntityOrNullAsync(string tableName, string partitionKey, string rowKey)
    {
        var table = TryGetTable(tableName);
        if (table != null)
        {
            var resp = await table.GetEntityIfExistsAsync<TableEntity>(partitionKey, rowKey);
            if (resp.HasValue && resp.Value != null)
            {
                GetMemoryTable(tableName)[MakeKey(partitionKey, rowKey)] = resp.Value;
                return resp.Value;
            }
            return null;
        }

        return GetMemoryTable(tableName).TryGetValue(MakeKey(partitionKey, rowKey), out var mem) ? mem : null;
    }

    public async Task<List<TableEntity>> QueryAllEntitiesAsync(string tableName)
    {
        var table = TryGetTable(tableName);
        if (table != null)
        {
            var list = new List<TableEntity>();
            await foreach (var entity in table.QueryAsync<TableEntity>())
            {
                list.Add(entity);
                GetMemoryTable(tableName)[MakeKey(entity.PartitionKey, entity.RowKey)] = entity;
            }
            return list;
        }

        return GetMemoryTable(tableName).Values.ToList();
    }

    public async Task UpsertAsync(string tableName, TableEntity entity, TableUpdateMode mode = TableUpdateMode.Replace, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entity.PartitionKey))
            throw new ArgumentException("PartitionKey is required.", nameof(entity));
        if (string.IsNullOrWhiteSpace(entity.RowKey))
            throw new ArgumentException("RowKey is required.", nameof(entity));

        var table = TryGetTable(tableName);
        if (table != null)
        {
            await table.UpsertEntityAsync(entity, mode, ct);
        }

        var memTable = GetMemoryTable(tableName);
        var key = MakeKey(entity.PartitionKey, entity.RowKey);
        if (mode == TableUpdateMode.Merge && memTable.TryGetValue(key, out var existing))
        {
            foreach (var kv in entity)
            {
                existing[kv.Key] = kv.Value;
            }
            memTable[key] = existing;
        }
        else
        {
            memTable[key] = entity;
        }
    }

    public async Task<bool> DeleteAsync(string tableName, string partitionKey, string rowKey)
    {
        var table = TryGetTable(tableName);
        if (table != null)
        {
            await table.DeleteEntityAsync(partitionKey, rowKey);
        }
        return GetMemoryTable(tableName).TryRemove(MakeKey(partitionKey, rowKey), out _);
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
    public TableClient Notifications => GetTable("notifications");
}
