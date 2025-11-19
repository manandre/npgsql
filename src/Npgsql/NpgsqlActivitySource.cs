using Npgsql.Internal;
using System;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Npgsql;

static class NpgsqlActivitySource
{
    static readonly ActivitySource Source = new("Npgsql", GetLibraryVersion());

    internal static bool IsEnabled => Source.HasListeners();

    internal static Activity? CommandStart(NpgsqlConnectionStringBuilder settings, string commandText, CommandType commandType, string? spanName)
    {
        var dbName = settings.Database ?? "UNKNOWN";
        string? dbOperation = null;
        string? dbSqlTable = null;
        string activityName;
        switch (commandType)
        {
        case CommandType.StoredProcedure:
            dbOperation = NpgsqlCommand.EnableStoredProcedureCompatMode ? "SELECT" : "CALL";
            // In this case our activity name follows the concept of the CommandType.TableDirect case
            // ("<db.operation> <db.name>.<db.sql.table>") but replaces db.sql.table with the procedure name
            // which seems to match the spec's intent without being explicitly specified that way (it suggests
            // using the procedure name but doesn't mention using db.operation or db.name in that case).
            activityName = $"{dbOperation} {dbName}.{commandText}";
            break;
        case CommandType.TableDirect:
            dbOperation = "SELECT";
            // The OpenTelemetry spec actually asks to include the database name into db.sql.table
            // but then again mixes the concept of database and schema.
            // As I interpret it, it actually wants db.sql.table to include the schema name and not the
            // database name if the concept of schemas exists in the database system.
            // This also makes sense in the context of the activity name which otherwise would include the
            // database name twice.
            dbSqlTable = commandText;
            activityName = $"{dbOperation} {dbName}.{dbSqlTable}";
            break;
        case CommandType.Text:
            activityName = dbName;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(commandType), commandType, null);
        }

        var activity = Source.StartActivity(spanName ?? activityName, ActivityKind.Client);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetTag("db.statement", commandText);

        if (dbOperation != null)
            activity.SetTag("db.operation", dbOperation);
        if (dbSqlTable != null)
            activity.SetTag("db.sql.table", dbSqlTable);

        return activity;
    }

    internal static Activity? ConnectionOpen(NpgsqlConnector connector)
    {
        if (!connector.DataSource.Configuration.TracingOptions.EnablePhysicalOpenTracing)
            return null;

        var dbName = connector.Settings.Database ?? connector.InferredUserName;
        var activity = Source.StartActivity(dbName, ActivityKind.Client);
        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetTag("db.system", "postgresql");
        activity.SetTag("db.connection_string", connector.UserFacingConnectionString);

        return activity;
    }

    internal static void Enrich(Activity activity, NpgsqlConnector connector)
    {
        if (!activity.IsAllDataRequested)
            return;

        activity.SetTag("db.system", "postgresql");
        activity.SetTag("db.connection_string", connector.UserFacingConnectionString);
        activity.SetTag("db.user", connector.InferredUserName);
        // We trace the actual (maybe inferred) database name we're connected to, even if it
        // wasn't specified in the connection string
        activity.SetTag("db.name", connector.Settings.Database ?? connector.InferredUserName);
        activity.SetTag("db.connection_id", connector.Id);

        var endPoint = connector.ConnectedEndPoint;
        Debug.Assert(endPoint is not null);
        switch (endPoint)
        {
        case IPEndPoint ipEndPoint:
            activity.SetTag("net.transport", "ip_tcp");
            activity.SetTag("net.peer.ip", ipEndPoint.Address.ToString());
            if (ipEndPoint.Port != 5432)
                activity.SetTag("net.peer.port", ipEndPoint.Port);
            activity.SetTag("net.peer.name", connector.Host);
            break;

        case UnixDomainSocketEndPoint:
            activity.SetTag("net.transport", "unix");
            activity.SetTag("net.peer.name", connector.Host);
            break;

        default:
            throw new ArgumentOutOfRangeException("Invalid endpoint type: " + endPoint.GetType());
        }
    }

    internal static void ReceivedFirstResponse(Activity activity, NpgsqlTracingOptions tracingOptions)
    {
        if (!activity.IsAllDataRequested || !tracingOptions.EnableFirstResponseEvent)
            return;

        var activityEvent = new ActivityEvent("received-first-response");
        activity.AddEvent(activityEvent);
    }

    internal static void CommandStop(Activity activity)
    {
        activity.SetStatus(ActivityStatusCode.Ok);
        activity.Dispose();
    }

    internal static void SetException(Activity activity, Exception ex, bool escaped = true)
    {
        // TODO: We can instead use Activity.AddException whenever we start using .NET 9
        var tags = new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.ToString() },
            { "exception.escaped", escaped }
        };
        var activityEvent = new ActivityEvent("exception", tags: tags);
        activity.AddEvent(activityEvent);
        var statusDescription = ex is PostgresException pgEx ? pgEx.SqlState : ex.Message;
        activity.SetStatus(ActivityStatusCode.Error, statusDescription);
        activity.Dispose();
    }

    internal static Activity? CopyStart(string command, NpgsqlConnector connector, string? spanName, string? operation = null)
    {
        var dbName = connector.Settings.Database ?? "UNKNOWN";
        var activity = Source.StartActivity(spanName ?? dbName, ActivityKind.Client);
        if (activity is not { IsAllDataRequested: true })
            return activity;
        activity.SetTag("db.statement", command);
        if (operation is not null)
            activity.SetTag("db.operation", operation);
        Enrich(activity, connector);
        return activity;
    }

    internal static Activity? ImportStart(string copyFromCommand, NpgsqlConnector connector, string? spanName)
        => CopyStart(copyFromCommand, connector, spanName, "COPY FROM");

    internal static Activity? ExportStart(string copyToCommand, NpgsqlConnector connector, string? spanName)
        => CopyStart(copyToCommand, connector, spanName, "COPY TO");

    internal static void SetImport(Activity activity)
        => SetOperation(activity, "COPY FROM");

    internal static void SetExport(Activity activity)
        => SetOperation(activity, "COPY TO");

    static void SetOperation(Activity activity, string operation)
    {
        if (!activity.IsAllDataRequested)
            return;
        activity.SetTag("db.operation", operation);
    }

    private static void CopyStop(Activity activity, ulong? rows = null)
    {
        activity.SetStatus(ActivityStatusCode.Ok);
        if (rows.HasValue)
            activity.SetTag("db.rows", rows.Value);
        activity.Dispose();
    }

    internal static void ImportStop(Activity activity, ulong? rows = null)
        => CopyStop(activity, rows);

    internal static void ExportStop(Activity activity, ulong? rows = null)
        => CopyStop(activity, rows);

    internal static void SetCancelled(Activity activity)
    {
        activity.SetStatus(ActivityStatusCode.Error, "Cancelled");
        activity.Dispose();
    }

    static string GetLibraryVersion()
        => typeof(NpgsqlDataSource).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "UNKNOWN";
}
