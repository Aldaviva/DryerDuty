using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Tests;

internal interface IServiceHostInterceptor: IDisposable {

    IHost? host { get; }

    /// <summary>
    /// Useful for modifying registered services
    /// </summary>
    event EventHandler<HostBuilder>? hostBuilding;

    event EventHandler<IHost>? hostBuilt;

}

/// <summary>
/// <para>Get access to the <see cref="HostBuilder"/> and <see cref="IHost"/> created by <see cref="Host.CreateDefaultBuilder()"/>.</para>
/// <para>Similar to the <c>Microsoft.AspNetCore.Mvc.Testing</c> package (<see href="https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests"/>), but for console applications instead of ASP.NET Core webapps.</para>
/// </summary>
internal class ServiceHostInterceptor: IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IServiceHostInterceptor {

    private readonly Stack<IDisposable> toDispose = new();

    public IHost? host { get; private set; }

    public event EventHandler<HostBuilder>? hostBuilding;

    public event EventHandler<IHost>? hostBuilt;

    public ServiceHostInterceptor() {
        toDispose.Push(DiagnosticListener.AllListeners.Subscribe(this));
    }

    public void OnCompleted() { }

    public void OnError(Exception error) { }

    public void OnNext(DiagnosticListener listener) {
        if (listener.Name == "Microsoft.Extensions.Hosting") {
            toDispose.Push(listener.Subscribe(this));
        }
    }

    public void OnNext(KeyValuePair<string, object?> diagnosticEvent) {
        switch (diagnosticEvent.Key) {
            case "HostBuilding":
                // you can add, modify, and remove registered services here
                hostBuilding?.Invoke(this, (HostBuilder) diagnosticEvent.Value!);
                break;
            case "HostBuilt":
                host = (IHost) diagnosticEvent.Value!;
                hostBuilt?.Invoke(this, host);
                break;
        }
    }

    public void Dispose() {
        while (toDispose.TryPop(out IDisposable? disposable)) {
            disposable.Dispose();
        }
    }

}