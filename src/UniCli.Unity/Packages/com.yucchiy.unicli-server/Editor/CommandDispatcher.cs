using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniCli.Protocol;
using UniCli.Server.Editor.Handlers;
using UnityEditor;
using UnityEngine;

namespace UniCli.Server.Editor
{
    public sealed class CommandDispatcher
    {
        private readonly Dictionary<string, ICommandHandler> _handlers =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly StringBuilder _formatBuffer = new();
        private readonly Lazy<CommandInfo[]> _commandInfoCache;

        private static readonly string ServerVersion = ResolveServerVersion();

        // Update on release: clients older than this version will be rejected
        private const string MinimumClientVersion = "0.11.1";

        private static string ResolveServerVersion()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(CommandDispatcher).Assembly);
            return packageInfo?.version ?? "unknown";
        }

        public CommandDispatcher(ServiceRegistry services)
        {
            services.AddSingleton(this);
            RegisterClassHandlers(services);
            _commandInfoCache = new Lazy<CommandInfo[]>(
                () => _handlers.Values.Select(h => h.GetCommandInfo()).ToArray());
        }

        private void RegisterClassHandlers(ServiceRegistry services)
        {
            var handlerTypes = TypeCache.GetTypesDerivedFrom<ICommandHandler>();
            var settings = services.Resolve<UniCliSettings>();

            foreach (var type in handlerTypes)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (type.Assembly.GetName().Name.Contains(".Tests"))
                    continue;

                var moduleName = ModuleRegistry.ResolveModuleName(type);
                if (moduleName != null && settings != null && !settings.IsModuleEnabled(moduleName))
                    continue;

                ICommandHandler handler;
                try
                {
                    var instance = services.CreateInstance(type);
                    if (instance == null)
                    {
                        UniCliEditorLog.LogWarning($"[UniCli] Failed to create handler instance: {type.FullName} (unresolvable constructor parameters)");
                        continue;
                    }
                    handler = (ICommandHandler)instance;
                }
                catch (Exception ex)
                {
                    UniCliEditorLog.LogWarning($"[UniCli] Failed to create handler instance: {type.FullName} ({ex.Message})");
                    continue;
                }

                if (!_handlers.TryAdd(handler.CommandName, handler))
                {
                    UniCliEditorLog.LogWarning($"[UniCli] Command '{handler.CommandName}' is already registered, skipping {type.FullName}");
                }
            }
        }

        public CommandInfo[] GetAllCommandInfo() => _commandInfoCache.Value;

        public async ValueTask<CommandResponse> DispatchAsync(CommandRequest request, CancellationToken cancellationToken)
        {
            if (!_handlers.TryGetValue(request.command, out var handler))
                return MakeResponse(false, $"Unknown command: {request.command}");

            var versionCheck = CheckVersionCompatibility(request.clientVersion);
            if (versionCheck.IsError)
                return MakeResponse(false, versionCheck.Message);

            var wantsText = request.format == "text";

            try
            {
                var result = await handler.ExecuteAsync(request, cancellationToken);
                var response = BuildResponse(true, $"Command '{request.command}' succeeded", result, handler, wantsText);
                response.versionWarning = versionCheck.Warning;
                return response;
            }
            catch (CommandFailedException ex)
            {
                var response = BuildResponse(false, $"Command failed: {ex.Message}", ex.ResponseData, handler, wantsText);
                response.versionWarning = versionCheck.Warning;
                return response;
            }
            catch (Exception ex)
            {
                var response = MakeResponse(false, $"Command failed: {ex.Message}");
                response.versionWarning = versionCheck.Warning;
                return response;
            }
        }

        private readonly struct VersionCheckResult
        {
            public readonly bool IsError;
            public readonly string Message;
            public readonly string Warning;

            public VersionCheckResult(bool isError, string message, string warning)
            {
                IsError = isError;
                Message = message;
                Warning = warning;
            }
        }

        private static VersionCheckResult CheckVersionCompatibility(string clientVersion)
        {
            if (string.IsNullOrEmpty(clientVersion))
                return new VersionCheckResult(false, "",
                    $"Unknown client version. Server is v{ServerVersion} (minimum client: v{MinimumClientVersion}). Please update unicli CLI.");

            if (!Version.TryParse(clientVersion, out var client) ||
                !Version.TryParse(MinimumClientVersion, out var minimum))
                return default;

            if (client < minimum)
                return new VersionCheckResult(true,
                    $"Client v{clientVersion} is below minimum supported v{MinimumClientVersion} (server v{ServerVersion}). Please update unicli CLI.",
                    "");

            return default;
        }

        internal CommandResponse BuildResponse(bool success, string message, object data, ICommandHandler handler, bool wantsText)
        {
            if (data is Unit or null)
                return MakeResponse(success, message);

            if (wantsText && handler is IResponseFormatter formatter)
            {
                _formatBuffer.Clear();
                var writer = new StringFormatWriter(_formatBuffer);
                if (formatter.TryWriteFormatted(data, success, writer))
                    return MakeResponse(success, message, _formatBuffer.ToString(), "text");
            }

            var json = data is IRawJsonResponse rawJson
                ? rawJson.ToJson()
                : data is CommandListResponse commandList
                    ? SerializeCommandListResponse(commandList)
                    : JsonUtility.ToJson(data);

            return MakeResponse(success, message, json);
        }

        private static string SerializeCommandListResponse(CommandListResponse response)
        {
            var builder = new StringBuilder(1024);
            builder.Append('{');
            WritePropertyName(builder, "commands");
            WriteCommandArray(builder, response?.commands);
            builder.Append('}');
            return builder.ToString();
        }

        private static void WriteCommandArray(StringBuilder builder, CommandInfo[] commands)
        {
            builder.Append('[');

            if (commands != null)
            {
                for (var i = 0; i < commands.Length; i++)
                {
                    if (i > 0)
                        builder.Append(',');

                    WriteCommand(builder, commands[i]);
                }
            }

            builder.Append(']');
        }

        private static void WriteCommand(StringBuilder builder, CommandInfo command)
        {
            builder.Append('{');
            WriteStringProperty(builder, "name", command?.name);
            builder.Append(',');
            WriteStringProperty(builder, "description", command?.description);
            builder.Append(',');
            WriteBooleanProperty(builder, "builtIn", command?.builtIn ?? false);
            builder.Append(',');
            WriteStringProperty(builder, "module", command?.module);
            builder.Append(',');
            WriteFieldArrayProperty(builder, "requestFields", command?.requestFields);
            builder.Append(',');
            WriteFieldArrayProperty(builder, "responseFields", command?.responseFields);
            builder.Append('}');
        }

        private static void WriteFieldArrayProperty(StringBuilder builder, string propertyName, CommandFieldInfo[] fields)
        {
            WritePropertyName(builder, propertyName);
            WriteFieldArray(builder, fields);
        }

        private static void WriteFieldArray(StringBuilder builder, CommandFieldInfo[] fields)
        {
            builder.Append('[');

            if (fields != null)
            {
                for (var i = 0; i < fields.Length; i++)
                {
                    if (i > 0)
                        builder.Append(',');

                    WriteField(builder, fields[i]);
                }
            }

            builder.Append(']');
        }

        private static void WriteField(StringBuilder builder, CommandFieldInfo field)
        {
            builder.Append('{');
            WriteStringProperty(builder, "name", field?.name);
            builder.Append(',');
            WriteStringProperty(builder, "type", field?.type);
            builder.Append(',');
            WriteStringProperty(builder, "defaultValue", field?.defaultValue);
            builder.Append(',');
            WriteFieldArrayProperty(builder, "children", field?.children);
            builder.Append('}');
        }

        private static void WriteStringProperty(StringBuilder builder, string propertyName, string value)
        {
            WritePropertyName(builder, propertyName);
            WriteJsonString(builder, value ?? "");
        }

        private static void WriteBooleanProperty(StringBuilder builder, string propertyName, bool value)
        {
            WritePropertyName(builder, propertyName);
            builder.Append(value ? "true" : "false");
        }

        private static void WritePropertyName(StringBuilder builder, string propertyName)
        {
            WriteJsonString(builder, propertyName);
            builder.Append(':');
        }

        private static void WriteJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');

            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (ch < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(ch);
                        }
                        break;
                }
            }

            builder.Append('"');
        }

        internal static CommandResponse MakeResponse(bool success, string message, string data = "", string format = "json")
        {
            return new CommandResponse
            {
                success = success,
                message = message,
                data = data,
                format = format,
                serverVersion = ServerVersion
            };
        }
    }
}
