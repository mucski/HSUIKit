using System;
using System.Reflection;
using UnityEngine;

namespace NeoUIManager.Integration
{
    // Compile this file into the consumer. No reference to NeoUIManager.dll is required.
    internal sealed class NeoUiPluginWindowClient : IDisposable
    {
        private readonly Type _api;
        private readonly long _token;
        private bool _disposed;

        private NeoUiPluginWindowClient(Type api, long token)
        {
            _api = api;
            _token = token;
        }

        internal static bool TryRegister(object owner, string pluginId, string displayName,
            string pluginVersion, Texture2D icon, string defaultBinding, Action open,
            Action toggle, Func<bool> canOpen, out NeoUiPluginWindowClient client, out string error)
        {
            client = null;
            error = string.Empty;
            try
            {
                Type api = ResolveApi();
                if (api == null)
                {
                    error = "NeoUIManager with window API v1 is required. Install/update the manager from the plugin pack.";
                    return false;
                }
                MethodInfo supports = api.GetMethod("SupportsVersion", new Type[] { typeof(int) });
                if (supports == null || !(bool)supports.Invoke(null, new object[] { 1 }))
                {
                    error = "This NeoUIManager does not support window API v1. Update the manager from the plugin pack.";
                    return false;
                }
                MethodInfo register = api.GetMethod("TryRegisterWindow", new Type[]
                {
                    typeof(int), typeof(object), typeof(string), typeof(string), typeof(string),
                    typeof(Texture2D), typeof(string), typeof(Action), typeof(Action), typeof(Func<bool>),
                    typeof(long).MakeByRefType(), typeof(string).MakeByRefType()
                });
                if (register == null) throw new MissingMethodException("NeoUIManager window API v1 registration is unavailable.");
                object[] arguments = new object[]
                {
                    1, owner, pluginId, displayName, pluginVersion, icon, defaultBinding,
                    open, toggle, canOpen, (long)0, string.Empty
                };
                bool registered = (bool)register.Invoke(null, arguments);
                error = (string)arguments[11];
                if (registered) client = new NeoUiPluginWindowClient(api, (long)arguments[10]);
                return registered;
            }
            catch (Exception ex)
            {
                error = Describe(ex);
                return false;
            }
        }

        internal bool IsActive
        {
            get
            {
                if (_disposed) return false;
                try
                {
                    return (bool)_api.GetMethod("IsRegistrationActive", new Type[] { typeof(long) })
                        .Invoke(null, new object[] { _token });
                }
                catch { return false; }
            }
        }

        internal bool TryGetBinding(out string binding, out string error)
        {
            binding = string.Empty;
            error = string.Empty;
            if (_disposed)
            {
                error = "Window registration has been disposed.";
                return false;
            }
            try
            {
                object[] arguments = new object[] { _token, string.Empty, string.Empty };
                bool found = (bool)_api.GetMethod("TryGetBinding", new Type[]
                {
                    typeof(long), typeof(string).MakeByRefType(), typeof(string).MakeByRefType()
                }).Invoke(null, arguments);
                binding = (string)arguments[1];
                error = (string)arguments[2];
                return found;
            }
            catch (Exception ex)
            {
                error = Describe(ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                _api.GetMethod("UnregisterWindow", new Type[] { typeof(long) })
                    .Invoke(null, new object[] { _token });
            }
            finally { _disposed = true; }
        }

        private static string Describe(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            if (invocation != null && invocation.InnerException != null) exception = invocation.InnerException;
            return "NeoUIManager window integration failed: " + exception.Message;
        }

        private static Type ResolveApi()
        {
            // IPA/LoadFrom assemblies need not be visible to a simple-name Type.GetType bind.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "NeoUIManager")
                    return assembly.GetType("NeoUIManager.NeoUiPluginWindowApi", false);
            return Type.GetType("NeoUIManager.NeoUiPluginWindowApi, NeoUIManager", false);
        }
    }
}
