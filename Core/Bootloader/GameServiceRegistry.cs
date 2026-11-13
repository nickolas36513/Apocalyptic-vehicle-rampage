
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameServiceRegistry
{
    private sealed class ServiceRegistration
    {
        public Type ServiceType;
        public IGameService Service;
        public Type[] Dependencies;
    }

    private readonly Dictionary<Type, object> services = new();
    private readonly Dictionary<Type, ServiceRegistration> managedServices = new();
    private readonly List<ServiceRegistration> registrationOrder = new();
    private readonly List<ServiceRegistration> initializedOrder = new();

    private bool isInitializing;
    private bool isInitialized;
    private bool isShuttingDown;

    public bool IsInitialized => isInitialized;
    public bool IsInitializing => isInitializing;
    public bool IsShuttingDown => isShuttingDown;
    public int Count => services.Count;
    public Exception InitializationError { get; private set; }

    public void Register<T>(T service, params Type[] dependencies)
        where T : class
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        if (isInitializing || isInitialized || isShuttingDown)
        {
            throw new InvalidOperationException(
                "Services cannot be registered during initialization, " +
                "after initialization, or during shutdown.");
        }

        Type serviceType = typeof(T);

        if (services.ContainsKey(serviceType))
        {
            throw new InvalidOperationException(
                $"Service already registered: {serviceType.Name}");
        }

        Type[] dependencyTypes = dependencies != null
            ? (Type[])dependencies.Clone()
            : Array.Empty<Type>();

        if (service is IGameService gameService)
        {
            for (int i = 0; i < dependencyTypes.Length; i++)
            {
                if (dependencyTypes[i] == null)
                {
                    throw new ArgumentException(
                        $"{serviceType.Name} has a null dependency.",
                        nameof(dependencies));
                }

                if (dependencyTypes[i] == serviceType)
                {
                    throw new InvalidOperationException(
                        $"{serviceType.Name} cannot depend on itself.");
                }
            }

            var registration = new ServiceRegistration
            {
                ServiceType = serviceType,
                Service = gameService,
                Dependencies = dependencyTypes
            };

            managedServices.Add(serviceType, registration);
            registrationOrder.Add(registration);
        }
        else if (dependencyTypes.Length > 0)
        {
            throw new InvalidOperationException(
                $"{serviceType.Name} does not implement IGameService " +
                "and cannot declare initialization dependencies.");
        }

        services.Add(serviceType, service);
    }

    public bool TryGet<T>(out T service) where T : class
    {
        if (services.TryGetValue(typeof(T), out object instance))
        {
            service = instance as T;
            return service != null;
        }

        service = null;
        return false;
    }

    public T Get<T>() where T : class
    {
        if (TryGet<T>(out T service))
            return service;

        throw new InvalidOperationException(
            $"Service not registered: {typeof(T).Name}");
    }
    public bool Validate(out string error)
    {
        error = null;

        if (isInitializing || isInitialized || isShuttingDown)
        {
            error = "Cannot validate services during an active lifecycle operation.";
            return false;
        }

        try
        {
            // Performs dependency checks, missing-service checks,
            // and circular dependency detection without initializing anything.
            BuildInitializationOrder();

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }
    public bool InitializeAll()
    {
        if (isInitialized)
            return true;

        if (isInitializing || isShuttingDown)
            return false;

        isInitializing = true;
        InitializationError = null;

        try
        {
            List<ServiceRegistration> orderedServices =
                BuildInitializationOrder();

            foreach (ServiceRegistration registration in orderedServices)
            {
                // Track before initialization so rollback also attempts
                // cleanup if Initialize partially succeeds and then throws.
                initializedOrder.Add(registration);

                registration.Service.Initialize();
            }

            isInitialized = true;
            return true;
        }
        catch (Exception exception)
        {
            InitializationError = exception;
            Debug.LogException(exception);

            ShutdownInitializedServices();

            isInitialized = false;
            return false;
        }
        finally
        {
            isInitializing = false;
        }
    }

    public void ShutdownAll()
    {
        if (isInitializing)
        {
            Debug.LogWarning(
                "[GameServiceRegistry] Shutdown rejected during initialization.");
            return;
        }

        if (isShuttingDown)
            return;

        if (initializedOrder.Count == 0)
        {
            isInitialized = false;
            return;
        }

        isShuttingDown = true;

        try
        {
            ShutdownInitializedServices();
        }
        finally
        {
            isInitialized = false;
            isShuttingDown = false;
        }
    }

    private List<ServiceRegistration> BuildInitializationOrder()
    {
        var ordered = new List<ServiceRegistration>();
        var visitStates = new Dictionary<Type, byte>();

        foreach (ServiceRegistration registration in registrationOrder)
            Visit(registration, visitStates, ordered);

        return ordered;
    }

    private void Visit(
        ServiceRegistration registration,
        Dictionary<Type, byte> visitStates,
        List<ServiceRegistration> ordered)
    {
        Type serviceType = registration.ServiceType;

        if (visitStates.TryGetValue(serviceType, out byte state))
        {
            if (state == 1)
            {
                throw new InvalidOperationException(
                    $"Circular service dependency detected at {serviceType.Name}.");
            }

            if (state == 2)
                return;
        }

        visitStates[serviceType] = 1;

        foreach (Type dependencyType in registration.Dependencies)
        {
            if (dependencyType == null)
            {
                throw new InvalidOperationException(
                    $"{serviceType.Name} has a null dependency.");
            }

            if (!managedServices.TryGetValue(
                    dependencyType, out ServiceRegistration dependency))
            {
                throw new InvalidOperationException(
                    $"{serviceType.Name} requires {dependencyType.Name}, " +
                    "but that managed service is not registered.");
            }

            Visit(dependency, visitStates, ordered);
        }

        visitStates[serviceType] = 2;
        ordered.Add(registration);
    }

    private void ShutdownInitializedServices()
    {
        for (int i = initializedOrder.Count - 1; i >= 0; i--)
        {
            try
            {
                initializedOrder[i].Service.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        initializedOrder.Clear();
    }

    public bool Unregister<T>() where T : class
    {
        if (isInitializing || isInitialized || isShuttingDown)
            return false;

        Type serviceType = typeof(T);

        if (!services.ContainsKey(serviceType))
            return false;

        // Don't remove a service that another registered service requires.
        foreach (ServiceRegistration registration in registrationOrder)
        {
            if (registration.ServiceType == serviceType)
                continue;

            foreach (Type dependency in registration.Dependencies)
            {
                if (dependency == serviceType)
                {
                    Debug.LogWarning(
                        $"Cannot unregister {serviceType.Name}: " +
                        $"{registration.ServiceType.Name} depends on it.");

                    return false;
                }
            }
        }

        services.Remove(serviceType);
        managedServices.Remove(serviceType);

        registrationOrder.RemoveAll(
            registration => registration.ServiceType == serviceType);

        return true;
    }

    public void Clear()
    {
        if (isInitializing || isShuttingDown)
        {
            Debug.LogWarning(
                "[GameServiceRegistry] Clear rejected during a lifecycle operation.");
            return;
        }

        if (isInitialized || initializedOrder.Count > 0)
            ShutdownAll();

        // Shutdown callbacks must not prevent registry cleanup.
        services.Clear();
        managedServices.Clear();
        registrationOrder.Clear();
        initializedOrder.Clear();

        isInitialized = false;
    }
}
