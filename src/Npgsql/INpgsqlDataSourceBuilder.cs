using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Npgsql
{
    /// <summary>
    /// A data source builder
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface INpgsqlDataSourceBuilder<out T>
    {
        /// <summary>
        /// Builds and returns an <see cref="NpgsqlDataSource" /> which is ready for use.
        /// </summary>
        NpgsqlDataSource Build();

        /// <summary>
        /// Builds and returns a <see cref="NpgsqlMultiHostDataSource" /> which is ready for use for load-balancing and failover scenarios.
        /// </summary>
        NpgsqlMultiHostDataSource BuildMultiHost();

        /// <summary>
        /// Sets the <see cref="ILoggerFactory" /> that will be used for logging.
        /// </summary>
        /// <param name="loggerFactory">The logger factory to be used.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        T UseLoggerFactory(ILoggerFactory? loggerFactory);

        /// <summary>
        /// Enables parameters to be included in logging. This includes potentially sensitive information from data sent to PostgreSQL.
        /// You should only enable this flag in development, or if you have the appropriate security measures in place based on the
        /// sensitivity of this data.
        /// </summary>
        /// <param name="parameterLoggingEnabled">If <see langword="true" />, then sensitive data is logged.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        T EnableParameterLogging(bool parameterLoggingEnabled = true);

        /// <summary>
        /// When using SSL/TLS, this is a callback that allows customizing how the PostgreSQL-provided certificate is verified. This is an
        /// advanced API, consider using <see cref="SslMode.VerifyFull" /> or <see cref="SslMode.VerifyCA" /> instead.
        /// </summary>
        /// <param name="userCertificateValidationCallback">The callback containing custom callback verification logic.</param>
        /// <remarks>
        /// <para>
        /// Cannot be used in conjunction with <see cref="SslMode.Disable" />, <see cref="SslMode.VerifyCA" /> or
        /// <see cref="SslMode.VerifyFull" />.
        /// </para>
        /// <para>
        /// See <see href="https://msdn.microsoft.com/en-us/library/system.net.security.remotecertificatevalidationcallback(v=vs.110).aspx"/>.
        /// </para>
        /// </remarks>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        T UseUserCertificateValidationCallback(RemoteCertificateValidationCallback userCertificateValidationCallback);

        /// <summary>
        /// Specifies an SSL/TLS certificate which Npgsql will send to PostgreSQL for certificate-based authentication.
        /// </summary>
        /// <param name="clientCertificate">The client certificate to be sent to PostgreSQL when opening a connection.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        T UseClientCertificate(X509Certificate? clientCertificate);

        /// <summary>
        /// Specifies a collection of SSL/TLS certificates which Npgsql will send to PostgreSQL for certificate-based authentication.
        /// </summary>
        /// <param name="clientCertificates">The client certificate collection to be sent to PostgreSQL when opening a connection.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        T UseClientCertificates(X509CertificateCollection? clientCertificates);

        /// <summary>
        /// Specifies a callback to modify the collection of SSL/TLS client certificates which Npgsql will send to PostgreSQL for
        /// certificate-based authentication. This is an advanced API, consider using <see cref="UseClientCertificate" /> or
        /// <see cref="UseClientCertificates" /> instead.
        /// </summary>
        /// <param name="clientCertificatesCallback">The callback to modify the client certificate collection.</param>
        /// <remarks>
        /// <para>
        /// The callback is invoked every time a physical connection is opened, and is therefore suitable for rotating short-lived client
        /// certificates. Simply make sure the certificate collection argument has the up-to-date certificate(s).
        /// </para>
        /// <para>
        /// The callback's collection argument already includes any client certificates specified via the connection string or environment
        /// variables.
        /// </para>
        /// </remarks>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        T UseClientCertificatesCallback(Action<X509CertificateCollection>? clientCertificatesCallback);

        /// <summary>
        /// Sets the <see cref="X509Certificate2" /> that will be used validate SSL certificate, received from the server.
        /// </summary>
        /// <param name="rootCertificate">The CA certificate.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        T UseRootCertificate(X509Certificate2? rootCertificate);

        /// <summary>
        /// Specifies a callback that will be used to validate SSL certificate, received from the server.
        /// </summary>
        /// <param name="rootCertificateCallback">The callback to get CA certificate.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        /// <remarks>
        /// This overload, which accepts a callback, is suitable for scenarios where the certificate rotates
        /// and might change during the lifetime of the application.
        /// When that's not the case, use the overload which directly accepts the certificate.
        /// </remarks>
        T UseRootCertificateCallback(Func<X509Certificate2>? rootCertificateCallback);

        /// <summary>
        /// Configures a periodic password provider, which is automatically called by the data source at some regular interval. This is the
        /// recommended way to fetch a rotating access token.
        /// </summary>
        /// <param name="passwordProvider">A callback which returns the password to be sent to PostgreSQL.</param>
        /// <param name="successRefreshInterval">How long to cache the password before re-invoking the callback.</param>
        /// <param name="failureRefreshInterval">
        /// If a password refresh attempt fails, it will be re-attempted with this interval.
        /// This should typically be much lower than <paramref name="successRefreshInterval" />.
        /// </param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        /// <remarks>
        /// <para>
        /// The provided callback is invoked in a timer, and not when opening connections. It therefore doesn't affect opening time.
        /// </para>
        /// <para>
        /// The provided cancellation token is only triggered when the entire data source is disposed. If you'd like to apply a timeout to the
        /// token fetching, do so within the provided callback.
        /// </para>
        /// </remarks>
        T UsePeriodicPasswordProvider(
            Func<NpgsqlConnectionStringBuilder, CancellationToken, ValueTask<string>>? passwordProvider,
            TimeSpan successRefreshInterval,
            TimeSpan failureRefreshInterval);

        /// <summary>
        /// Register a connection initializer, which allows executing arbitrary commands when a physical database connection is first opened.
        /// </summary>
        /// <param name="connectionInitializer">
        /// A synchronous connection initialization lambda, which will be called from <see cref="NpgsqlConnection.Open()" /> when a new physical
        /// connection is opened.
        /// </param>
        /// <param name="connectionInitializerAsync">
        /// An asynchronous connection initialization lambda, which will be called from
        /// <see cref="NpgsqlConnection.OpenAsync(CancellationToken)" /> when a new physical connection is opened.
        /// </param>
        /// <remarks>
        /// If an initializer is registered, both sync and async versions must be provided. If you do not use sync APIs in your code, simply
        /// throw <see cref="NotSupportedException" />, which would also catch accidental cases of sync opening.
        /// </remarks>
        /// <remarks>
        /// Take care that the setting you apply in the initializer does not get reverted when the connection is returned to the pool, since
        /// Npgsql sends <c>DISCARD ALL</c> by default. The <see cref="NpgsqlConnectionStringBuilder.NoResetOnClose" /> option can be used to
        /// turn this off.
        /// </remarks>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        T UsePhysicalConnectionInitializer(
            Action<NpgsqlConnection>? connectionInitializer,
            Func<NpgsqlConnection, Task>? connectionInitializerAsync);
    }
}
