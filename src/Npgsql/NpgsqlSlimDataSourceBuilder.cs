using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql.Internal;
using Npgsql.Internal.Resolvers;
using Npgsql.Properties;
using Npgsql.TypeMapping;

namespace Npgsql;

/// <summary>
/// Provides a simple API for configuring and creating an <see cref="NpgsqlDataSource" />, from which database connections can be obtained.
/// </summary>
/// <remarks>
/// On this builder, various features are disabled by default; unless you're looking to save on code size (e.g. when publishing with
/// NativeAOT), use <see cref="NpgsqlDataSourceBuilder" /> instead.
/// </remarks>
public sealed class NpgsqlSlimDataSourceBuilder : INpgsqlDataSourceBuilder<NpgsqlSlimDataSourceBuilder>, INpgsqlTypeMapper
{
    static UnsupportedTypeInfoResolver<NpgsqlSlimDataSourceBuilder> UnsupportedTypeInfoResolver { get; } = new();

    ILoggerFactory? _loggerFactory;
    bool _sensitiveDataLoggingEnabled;

    TransportSecurityHandler _transportSecurityHandler = new();
    RemoteCertificateValidationCallback? _userCertificateValidationCallback;
    Action<X509CertificateCollection>? _clientCertificatesCallback;

    IntegratedSecurityHandler _integratedSecurityHandler = new();

    Func<NpgsqlConnectionStringBuilder, CancellationToken, ValueTask<string>>? _periodicPasswordProvider;
    TimeSpan _periodicPasswordSuccessRefreshInterval, _periodicPasswordFailureRefreshInterval;

    readonly List<IPgTypeInfoResolver> _resolverChain = new();
    readonly UserTypeMapper _userTypeMapper;

    Action<NpgsqlConnection>? _syncConnectionInitializer;
    Func<NpgsqlConnection, Task>? _asyncConnectionInitializer;

    /// <summary>
    /// A connection string builder that can be used to configured the connection string on the builder.
    /// </summary>
    public NpgsqlConnectionStringBuilder ConnectionStringBuilder { get; }

    /// <summary>
    /// Returns the connection string, as currently configured on the builder.
    /// </summary>
    public string ConnectionString => ConnectionStringBuilder.ToString();

    static NpgsqlSlimDataSourceBuilder()
        => GlobalTypeMapper.Instance.AddGlobalTypeMappingResolvers(new []
        {
            AdoTypeInfoResolver.Instance
        });

    /// <summary>
    /// A diagnostics name used by Npgsql when generating tracing, logging and metrics.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Constructs a new <see cref="NpgsqlSlimDataSourceBuilder" />, optionally starting out from the given
    /// <paramref name="connectionString"/>.
    /// </summary>
    public NpgsqlSlimDataSourceBuilder(string? connectionString = null)
    {
        ConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        _userTypeMapper = new();
        // Reverse order
        AddTypeInfoResolver(UnsupportedTypeInfoResolver);
        AddTypeInfoResolver(new AdoTypeInfoResolver());
        // When used publicly we start off with our slim defaults.
        var plugins = new List<IPgTypeInfoResolver>(GlobalTypeMapper.Instance.GetPluginResolvers());
        plugins.Reverse();
        foreach (var plugin in plugins)
            AddTypeInfoResolver(plugin);
    }

    internal NpgsqlSlimDataSourceBuilder(NpgsqlConnectionStringBuilder connectionStringBuilder)
    {
        ConnectionStringBuilder = connectionStringBuilder;
        _userTypeMapper = new();
    }

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UseLoggerFactory(ILoggerFactory? loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder EnableParameterLogging(bool parameterLoggingEnabled = true)
    {
        _sensitiveDataLoggingEnabled = parameterLoggingEnabled;
        return this;
    }

    #region Authentication

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UseUserCertificateValidationCallback(
        RemoteCertificateValidationCallback userCertificateValidationCallback)
    {
        _userCertificateValidationCallback = userCertificateValidationCallback;

        return this;
    }

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UseClientCertificate(X509Certificate? clientCertificate)
    {
        if (clientCertificate is null)
            return UseClientCertificatesCallback(null);

        var clientCertificates = new X509CertificateCollection { clientCertificate };
        return UseClientCertificates(clientCertificates);
    }

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UseClientCertificates(X509CertificateCollection? clientCertificates)
        => UseClientCertificatesCallback(clientCertificates is null ? null : certs => certs.AddRange(clientCertificates));

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UseClientCertificatesCallback(Action<X509CertificateCollection>? clientCertificatesCallback)
    {
        _clientCertificatesCallback = clientCertificatesCallback;

        return this;
    }

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UseRootCertificate(X509Certificate2? rootCertificate)
        => rootCertificate is null
            ? UseRootCertificateCallback(null)
            : UseRootCertificateCallback(() => rootCertificate);

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UseRootCertificateCallback(Func<X509Certificate2>? rootCertificateCallback)
    {
        _transportSecurityHandler.RootCertificateCallback = rootCertificateCallback;

        return this;
    }

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UsePeriodicPasswordProvider(
        Func<NpgsqlConnectionStringBuilder, CancellationToken, ValueTask<string>>? passwordProvider,
        TimeSpan successRefreshInterval,
        TimeSpan failureRefreshInterval)
    {
        if (successRefreshInterval < TimeSpan.Zero)
            throw new ArgumentException(
                string.Format(NpgsqlStrings.ArgumentMustBePositive, nameof(successRefreshInterval)), nameof(successRefreshInterval));
        if (failureRefreshInterval < TimeSpan.Zero)
            throw new ArgumentException(
                string.Format(NpgsqlStrings.ArgumentMustBePositive, nameof(failureRefreshInterval)), nameof(failureRefreshInterval));

        _periodicPasswordProvider = passwordProvider;
        _periodicPasswordSuccessRefreshInterval = successRefreshInterval;
        _periodicPasswordFailureRefreshInterval = failureRefreshInterval;

        return this;
    }

    #endregion Authentication

    #region Type mapping

    /// <inheritdoc />
    public INpgsqlNameTranslator DefaultNameTranslator { get; set; } = GlobalTypeMapper.Instance.DefaultNameTranslator;

    /// <inheritdoc />
    public INpgsqlTypeMapper MapEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
        where TEnum : struct, Enum
    {
        _userTypeMapper.MapEnum<TEnum>(pgName, nameTranslator);
        return this;
    }

    /// <inheritdoc />
    public bool UnmapEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
        where TEnum : struct, Enum
        => _userTypeMapper.UnmapEnum<TEnum>(pgName, nameTranslator);

    /// <inheritdoc />
    [RequiresDynamicCode("Mapping composite types involves serializing arbitrary types, requiring require creating new generic types or methods. This is currently unsupported with NativeAOT, vote on issue #5303 if this is important to you.")]
    public INpgsqlTypeMapper MapComposite<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(
        string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
    {
        _userTypeMapper.MapComposite(typeof(T), pgName, nameTranslator);
        return this;
    }

    /// <inheritdoc />
    [RequiresDynamicCode("Mapping composite types involves serializing arbitrary types, requiring require creating new generic types or methods. This is currently unsupported with NativeAOT, vote on issue #5303 if this is important to you.")]
    public bool UnmapComposite<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(
        string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
        => _userTypeMapper.UnmapComposite(typeof(T), pgName, nameTranslator);

    /// <inheritdoc />
    [RequiresDynamicCode("Mapping composite types involves serializing arbitrary types, requiring require creating new generic types or methods. This is currently unsupported with NativeAOT, vote on issue #5303 if this is important to you.")]
    public INpgsqlTypeMapper MapComposite([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
        Type clrType, string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
    {
        _userTypeMapper.MapComposite(clrType, pgName, nameTranslator);
        return this;
    }

    /// <inheritdoc />
    [RequiresDynamicCode("Mapping composite types involves serializing arbitrary types, requiring require creating new generic types or methods. This is currently unsupported with NativeAOT, vote on issue #5303 if this is important to you.")]
    public bool UnmapComposite([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
        Type clrType, string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
        => _userTypeMapper.UnmapComposite(clrType, pgName, nameTranslator);

    /// <summary>
    /// Adds a type info resolver which can add or modify support for PostgreSQL types.
    /// Typically used by plugins.
    /// </summary>
    /// <param name="resolver">The type resolver to be added.</param>
    public void AddTypeInfoResolver(IPgTypeInfoResolver resolver)
    {
        var type = resolver.GetType();

        for (var i = 0; i < _resolverChain.Count; i++)
            if (_resolverChain[i].GetType() == type)
            {
                _resolverChain.RemoveAt(i);
                break;
            }

        _resolverChain.Insert(0, resolver);
    }

    void INpgsqlTypeMapper.Reset()
        => ResetTypeMappings();

    internal void ResetTypeMappings()
    {
        _resolverChain.Clear();
        _resolverChain.AddRange(GlobalTypeMapper.Instance.GetPluginResolvers());
    }

    #endregion Type mapping

    #region Optional opt-ins

    /// <summary>
    /// Sets up mappings for the PostgreSQL <c>array</c> types.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableArrays()
    {
        AddTypeInfoResolver(new RangeArrayTypeInfoResolver());
        AddTypeInfoResolver(new ExtraConversionsArrayTypeInfoResolver());
        AddTypeInfoResolver(new AdoArrayTypeInfoResolver());
        return this;
    }

    /// <summary>
    /// Sets up mappings for the PostgreSQL <c>range</c> types.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableRanges()
    {
        AddTypeInfoResolver(new RangeTypeInfoResolver());
        return this;
    }

    /// <summary>
    /// Sets up mappings for the PostgreSQL <c>multirange</c> types.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableMultiranges()
    {
        AddTypeInfoResolver(new RangeTypeInfoResolver());
        return this;
    }

    /// <summary>
    /// Sets up mappings for the PostgreSQL <c>record</c> type as a .NET <c>object[]</c>.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableRecords()
    {
        AddTypeInfoResolver(new RecordTypeInfoResolver());
        return this;
    }

    /// <summary>
    /// Sets up mappings for the PostgreSQL <c>tsquery</c> and <c>tsvector</c> types.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableFullTextSearch()
    {
        AddTypeInfoResolver(new FullTextSearchTypeInfoResolver());
        return this;
    }

    /// <summary>
    /// Sets up mappings for the PostgreSQL <c>ltree</c> extension types.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableLTree()
    {
        AddTypeInfoResolver(new LTreeTypeInfoResolver());
        return this;
    }

    /// <summary>
    /// Sets up mappings for extra conversions from PostgreSQL to .NET types.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableExtraConversions()
    {
        AddTypeInfoResolver(new ExtraConversionsResolver());
        return this;
    }

    /// <summary>
    /// Enables the possibility to use TLS/SSl encryption for connections to PostgreSQL. This does not guarantee that encryption will
    /// actually be used; see <see href="https://www.npgsql.org/doc/security.html"/> for more details.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableTransportSecurity()
    {
        _transportSecurityHandler = new RealTransportSecurityHandler();
        return this;
    }

    /// <summary>
    /// Enables the possibility to use GSS/SSPI authentication for connections to PostgreSQL. This does not guarantee that it will
    /// actually be used; see <see href="https://www.npgsql.org/doc/security.html"/> for more details.
    /// </summary>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public NpgsqlSlimDataSourceBuilder EnableIntegratedSecurity()
    {
        _integratedSecurityHandler = new RealIntegratedSecurityHandler();
        return this;
    }

    #endregion Optional opt-ins

    ///<inheritdoc/>
    public NpgsqlSlimDataSourceBuilder UsePhysicalConnectionInitializer(
        Action<NpgsqlConnection>? connectionInitializer,
        Func<NpgsqlConnection, Task>? connectionInitializerAsync)
    {
        if (connectionInitializer is null != connectionInitializerAsync is null)
            throw new ArgumentException(NpgsqlStrings.SyncAndAsyncConnectionInitializersRequired);

        _syncConnectionInitializer = connectionInitializer;
        _asyncConnectionInitializer = connectionInitializerAsync;

        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSource Build()
    {
        var config = PrepareConfiguration();
        var connectionStringBuilder = ConnectionStringBuilder.Clone();

        if (ConnectionStringBuilder.Host!.Contains(","))
        {
            ValidateMultiHost();

            return new NpgsqlMultiHostDataSource(connectionStringBuilder, config);
        }

        return ConnectionStringBuilder.Multiplexing
            ? new MultiplexingDataSource(connectionStringBuilder, config)
            : ConnectionStringBuilder.Pooling
                ? new PoolingDataSource(connectionStringBuilder, config)
                : new UnpooledDataSource(connectionStringBuilder, config);
    }

    ///<inheritdoc/>
    public NpgsqlMultiHostDataSource BuildMultiHost()
    {
        var config = PrepareConfiguration();

        ValidateMultiHost();

        return new(ConnectionStringBuilder.Clone(), config);
    }

    NpgsqlDataSourceConfiguration PrepareConfiguration()
    {
        ConnectionStringBuilder.PostProcessAndValidate();

        if (!_transportSecurityHandler.SupportEncryption && (_userCertificateValidationCallback is not null || _clientCertificatesCallback is not null))
        {
            throw new InvalidOperationException(NpgsqlStrings.TransportSecurityDisabled);
        }

        if (_periodicPasswordProvider is not null &&
            (ConnectionStringBuilder.Password is not null || ConnectionStringBuilder.Passfile is not null))
        {
            throw new NotSupportedException(NpgsqlStrings.CannotSetBothPasswordProviderAndPassword);
        }

        return new(
            Name,
            _loggerFactory is null
                ? NpgsqlLoggingConfiguration.NullConfiguration
                : new NpgsqlLoggingConfiguration(_loggerFactory, _sensitiveDataLoggingEnabled),
            _transportSecurityHandler,
            _integratedSecurityHandler,
            _userCertificateValidationCallback,
            _clientCertificatesCallback,
            _periodicPasswordProvider,
            _periodicPasswordSuccessRefreshInterval,
            _periodicPasswordFailureRefreshInterval,
            Resolvers(),
            HackyEnumMappings(),
            DefaultNameTranslator,
            _syncConnectionInitializer,
            _asyncConnectionInitializer);

        IEnumerable<IPgTypeInfoResolver> Resolvers()
        {
            var resolvers = new List<IPgTypeInfoResolver>();

            if (_userTypeMapper.Items.Count > 0)
                resolvers.Add(_userTypeMapper.Build());

            if (GlobalTypeMapper.Instance.GetUserMappingsResolver() is { } globalUserTypeMapper)
                resolvers.Add(globalUserTypeMapper);

            resolvers.AddRange(_resolverChain);

            return resolvers;
        }

        List<HackyEnumTypeMapping> HackyEnumMappings()
        {
            var mappings = new List<HackyEnumTypeMapping>();

            if (_userTypeMapper.Items.Count > 0)
                foreach (var userTypeMapping in _userTypeMapper.Items)
                    if (userTypeMapping is UserTypeMapper.EnumMapping enumMapping)
                        mappings.Add(new(enumMapping.ClrType, enumMapping.PgTypeName, enumMapping.NameTranslator));

            if (GlobalTypeMapper.Instance.HackyEnumTypeMappings.Count > 0)
                mappings.AddRange(GlobalTypeMapper.Instance.HackyEnumTypeMappings);

            return mappings;
        }
    }

    void ValidateMultiHost()
    {
        if (ConnectionStringBuilder.TargetSessionAttributes is not null)
            throw new InvalidOperationException(NpgsqlStrings.CannotSpecifyTargetSessionAttributes);
        if (ConnectionStringBuilder.Multiplexing)
            throw new NotSupportedException("Multiplexing is not supported with multiple hosts");
        if (ConnectionStringBuilder.ReplicationMode != ReplicationMode.Off)
            throw new NotSupportedException("Replication is not supported with multiple hosts");
    }
}
