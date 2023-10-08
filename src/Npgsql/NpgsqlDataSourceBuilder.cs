using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql.Internal;
using Npgsql.Internal.Resolvers;
using Npgsql.TypeMapping;

namespace Npgsql;

/// <summary>
/// Provides a simple API for configuring and creating an <see cref="NpgsqlDataSource" />, from which database connections can be obtained.
/// </summary>
public sealed class NpgsqlDataSourceBuilder : INpgsqlDataSourceBuilder<NpgsqlDataSourceBuilder>, INpgsqlTypeMapper
{
    static UnsupportedTypeInfoResolver<NpgsqlDataSourceBuilder> UnsupportedTypeInfoResolver { get; } = new();

    readonly NpgsqlSlimDataSourceBuilder _internalBuilder;

    /// <summary>
    /// A diagnostics name used by Npgsql when generating tracing, logging and metrics.
    /// </summary>
    public string? Name
    {
        get => _internalBuilder.Name;
        set => _internalBuilder.Name = value;
    }

    /// <inheritdoc />
    public INpgsqlNameTranslator DefaultNameTranslator
    {
        get => _internalBuilder.DefaultNameTranslator;
        set => _internalBuilder.DefaultNameTranslator = value;
    }

    /// <summary>
    /// A connection string builder that can be used to configured the connection string on the builder.
    /// </summary>
    public NpgsqlConnectionStringBuilder ConnectionStringBuilder => _internalBuilder.ConnectionStringBuilder;

    /// <summary>
    /// Returns the connection string, as currently configured on the builder.
    /// </summary>
    public string ConnectionString => _internalBuilder.ConnectionString;

    internal static void ResetGlobalMappings(bool overwrite)
        => GlobalTypeMapper.Instance.AddGlobalTypeMappingResolvers(new IPgTypeInfoResolver[]
        {
            overwrite ? new AdoTypeInfoResolver() : AdoTypeInfoResolver.Instance,
            new ExtraConversionsResolver(),
            new JsonTypeInfoResolver(),
            new RangeTypeInfoResolver(),
            new RecordTypeInfoResolver(),
            new FullTextSearchTypeInfoResolver(),
            new NetworkTypeInfoResolver(),
            new GeometricTypeInfoResolver(),
            new LTreeTypeInfoResolver(),

            // Arrays
            new AdoArrayTypeInfoResolver(),
            new ExtraConversionsArrayTypeInfoResolver(),
            new JsonArrayTypeInfoResolver(),
            new RangeArrayTypeInfoResolver(),
            new RecordArrayTypeInfoResolver(),
        }, overwrite);

    static NpgsqlDataSourceBuilder()
        => ResetGlobalMappings(overwrite: false);

    /// <summary>
    /// Constructs a new <see cref="NpgsqlDataSourceBuilder" />, optionally starting out from the given <paramref name="connectionString"/>.
    /// </summary>
    public NpgsqlDataSourceBuilder(string? connectionString = null)
    {
        _internalBuilder = new(new NpgsqlConnectionStringBuilder(connectionString));
        AddDefaultFeatures();

        void AddDefaultFeatures()
        {
            _internalBuilder.EnableTransportSecurity();
            _internalBuilder.EnableIntegratedSecurity();
            AddTypeInfoResolver(UnsupportedTypeInfoResolver);

            // Reverse order arrays.
            AddTypeInfoResolver(new RecordArrayTypeInfoResolver());
            AddTypeInfoResolver(new RangeArrayTypeInfoResolver());
            AddTypeInfoResolver(new JsonArrayTypeInfoResolver());
            AddTypeInfoResolver(new ExtraConversionsArrayTypeInfoResolver());
            AddTypeInfoResolver(new AdoArrayTypeInfoResolver());

            // Reverse order.
            AddTypeInfoResolver(new LTreeTypeInfoResolver());
            AddTypeInfoResolver(new GeometricTypeInfoResolver());
            AddTypeInfoResolver(new NetworkTypeInfoResolver());
            AddTypeInfoResolver(new FullTextSearchTypeInfoResolver());
            AddTypeInfoResolver(new RecordTypeInfoResolver());
            AddTypeInfoResolver(new RangeTypeInfoResolver());
            AddTypeInfoResolver(new JsonTypeInfoResolver());
            AddTypeInfoResolver(new ExtraConversionsResolver());
            AddTypeInfoResolver(AdoTypeInfoResolver.Instance);

            var plugins = new List<IPgTypeInfoResolver>(GlobalTypeMapper.Instance.GetPluginResolvers());
            plugins.Reverse();
            foreach (var plugin in plugins)
                AddTypeInfoResolver(plugin);
        }
    }

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UseLoggerFactory(ILoggerFactory? loggerFactory)
    {
        _internalBuilder.UseLoggerFactory(loggerFactory);
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder EnableParameterLogging(bool parameterLoggingEnabled = true)
    {
        _internalBuilder.EnableParameterLogging(parameterLoggingEnabled);
        return this;
    }

    #region Authentication

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UseUserCertificateValidationCallback(RemoteCertificateValidationCallback userCertificateValidationCallback)
    {
        _internalBuilder.UseUserCertificateValidationCallback(userCertificateValidationCallback);
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UseClientCertificate(X509Certificate? clientCertificate)
    {
        _internalBuilder.UseClientCertificate(clientCertificate);
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UseClientCertificates(X509CertificateCollection? clientCertificates)
    {
        _internalBuilder.UseClientCertificates(clientCertificates);
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UseClientCertificatesCallback(Action<X509CertificateCollection>? clientCertificatesCallback)
    {
        _internalBuilder.UseClientCertificatesCallback(clientCertificatesCallback);
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UseRootCertificate(X509Certificate2? rootCertificate)
    {
        _internalBuilder.UseRootCertificate(rootCertificate);
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UseRootCertificateCallback(Func<X509Certificate2>? rootCertificateCallback)
    {
        _internalBuilder.UseRootCertificateCallback(rootCertificateCallback);
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UsePeriodicPasswordProvider(
        Func<NpgsqlConnectionStringBuilder, CancellationToken, ValueTask<string>>? passwordProvider,
        TimeSpan successRefreshInterval,
        TimeSpan failureRefreshInterval)
    {
        _internalBuilder.UsePeriodicPasswordProvider(passwordProvider, successRefreshInterval, failureRefreshInterval);
        return this;
    }

    #endregion Authentication

    #region Type mapping

    /// <inheritdoc />
    public void AddTypeInfoResolver(IPgTypeInfoResolver resolver)
        => _internalBuilder.AddTypeInfoResolver(resolver);

    /// <inheritdoc />
    void INpgsqlTypeMapper.Reset()
        => _internalBuilder.ResetTypeMappings();

    /// <inheritdoc />
    public INpgsqlTypeMapper MapEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
        where TEnum : struct, Enum
    {
        _internalBuilder.MapEnum<TEnum>(pgName, nameTranslator);
        return this;
    }

    /// <inheritdoc />
    public bool UnmapEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
        where TEnum : struct, Enum
        => _internalBuilder.UnmapEnum<TEnum>(pgName, nameTranslator);

    /// <inheritdoc />
    [RequiresDynamicCode("Mapping composite types involves serializing arbitrary types, requiring require creating new generic types or methods. This is currently unsupported with NativeAOT, vote on issue #5303 if this is important to you.")]
    public INpgsqlTypeMapper MapComposite<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(
        string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
    {
        _internalBuilder.MapComposite<T>(pgName, nameTranslator);
        return this;
    }

    /// <inheritdoc />
    [RequiresDynamicCode("Mapping composite types involves serializing arbitrary types, requiring require creating new generic types or methods. This is currently unsupported with NativeAOT, vote on issue #5303 if this is important to you.")]
    public INpgsqlTypeMapper MapComposite([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
        Type clrType, string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
    {
        _internalBuilder.MapComposite(clrType, pgName, nameTranslator);
        return this;
    }

    /// <inheritdoc />
    [RequiresDynamicCode("Mapping composite types involves serializing arbitrary types, requiring require creating new generic types or methods. This is currently unsupported with NativeAOT, vote on issue #5303 if this is important to you.")]
    public bool UnmapComposite<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(
        string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
        => _internalBuilder.UnmapComposite<T>(pgName, nameTranslator);

    /// <inheritdoc />
    [RequiresDynamicCode("Mapping composite types involves serializing arbitrary types, requiring require creating new generic types or methods. This is currently unsupported with NativeAOT, vote on issue #5303 if this is important to you.")]
    public bool UnmapComposite([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
        Type clrType, string? pgName = null, INpgsqlNameTranslator? nameTranslator = null)
        => _internalBuilder.UnmapComposite(clrType, pgName, nameTranslator);

    #endregion Type mapping

    ///<inheritdoc/>
    public NpgsqlDataSourceBuilder UsePhysicalConnectionInitializer(
        Action<NpgsqlConnection>? connectionInitializer,
        Func<NpgsqlConnection, Task>? connectionInitializerAsync)
    {
        _internalBuilder.UsePhysicalConnectionInitializer(connectionInitializer, connectionInitializerAsync);
        return this;
    }

    ///<inheritdoc/>
    public NpgsqlDataSource Build()
        => _internalBuilder.Build();

    ///<inheritdoc/>
    public NpgsqlMultiHostDataSource BuildMultiHost()
        => _internalBuilder.BuildMultiHost();
}
