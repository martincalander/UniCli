using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UniCli.Protocol;
using UniCli.Server.Editor.Handlers;
using UnityEditor;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class CommandDispatcherResponseTests
    {
        [Serializable]
        private class TestResponse
        {
            public string value;
        }

        private class StubHandler : ICommandHandler
        {
            public string CommandName => "Test.Stub";
            public string Description => "Stub handler for tests";
            public CommandInfo GetCommandInfo() => new() { name = CommandName, description = Description };
            public ValueTask<object> ExecuteAsync(object request, CancellationToken cancellationToken) => default;
        }

        private class StubFormatterHandler : ICommandHandler, IResponseFormatter
        {
            public string CommandName => "Test.Formatter";
            public string Description => "Stub formatter handler";
            public CommandInfo GetCommandInfo() => new() { name = CommandName, description = Description };
            public ValueTask<object> ExecuteAsync(object request, CancellationToken cancellationToken) => default;

            public bool TryWriteFormatted(object response, bool success, IFormatWriter writer)
            {
                writer.WriteLine($"formatted:{((TestResponse)response).value}");
                return true;
            }
        }

        private static ServiceRegistry CreateServiceRegistry()
        {
            var services = new ServiceRegistry();
            var installerTypes = TypeCache.GetTypesDerivedFrom<IServiceInstaller>();
            foreach (var type in installerTypes)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                var installer = (IServiceInstaller)Activator.CreateInstance(type);
                installer.Install(services);
            }

            return services;
        }

        [Test]
        public void MakeResponse_Success_SetsFieldsCorrectly()
        {
            var response = CommandDispatcher.MakeResponse(true, "ok", "{}", "json");
            Assert.IsTrue(response.success);
            Assert.AreEqual("ok", response.message);
            Assert.AreEqual("{}", response.data);
            Assert.AreEqual("json", response.format);
        }

        [Test]
        public void MakeResponse_Error_SetsFieldsCorrectly()
        {
            var response = CommandDispatcher.MakeResponse(false, "fail");
            Assert.IsFalse(response.success);
            Assert.AreEqual("fail", response.message);
            Assert.AreEqual("", response.data);
            Assert.AreEqual("json", response.format);
        }

        [Test]
        public void BuildResponse_UnitData_ReturnsEmptyData()
        {
            var dispatcher = new CommandDispatcher(CreateServiceRegistry());
            var handler = new StubHandler();
            var response = dispatcher.BuildResponse(true, "ok", Unit.Value, handler, false);
            Assert.IsTrue(response.success);
            Assert.AreEqual("", response.data);
        }

        [Test]
        public void BuildResponse_NullData_ReturnsEmptyData()
        {
            var dispatcher = new CommandDispatcher(CreateServiceRegistry());
            var handler = new StubHandler();
            var response = dispatcher.BuildResponse(true, "ok", null, handler, false);
            Assert.IsTrue(response.success);
            Assert.AreEqual("", response.data);
        }

        [Test]
        public void BuildResponse_JsonMode_ReturnsJsonData()
        {
            var dispatcher = new CommandDispatcher(CreateServiceRegistry());
            var handler = new StubHandler();
            var data = new TestResponse { value = "hello" };
            var response = dispatcher.BuildResponse(true, "ok", data, handler, false);
            Assert.AreEqual("json", response.format);
            StringAssert.Contains("hello", response.data);
        }

        [Test]
        public void BuildResponse_TextMode_WithFormatter_ReturnsTextData()
        {
            var dispatcher = new CommandDispatcher(CreateServiceRegistry());
            var handler = new StubFormatterHandler();
            var data = new TestResponse { value = "world" };
            var response = dispatcher.BuildResponse(true, "ok", data, handler, true);
            Assert.AreEqual("text", response.format);
            StringAssert.Contains("formatted:world", response.data);
        }

        [Test]
        public void BuildResponse_TextMode_WithoutFormatter_FallsBackToJson()
        {
            var dispatcher = new CommandDispatcher(CreateServiceRegistry());
            var handler = new StubHandler();
            var data = new TestResponse { value = "fallback" };
            var response = dispatcher.BuildResponse(true, "ok", data, handler, true);
            Assert.AreEqual("json", response.format);
            StringAssert.Contains("fallback", response.data);
        }

        [Test]
        public void BuildResponse_CommandListResponse_WithDeepChildren_SerializesFullDepth()
        {
            var dispatcher = new CommandDispatcher(CreateServiceRegistry());
            var handler = new StubHandler();
            var data = new CommandListResponse
            {
                commands = new[]
                {
                    new CommandInfo
                    {
                        name = "Test.Command",
                        description = "Command metadata",
                        builtIn = true,
                        module = "",
                        requestFields = new[] { CreateFieldChain(12) },
                        responseFields = Array.Empty<CommandFieldInfo>()
                    }
                }
            };

            var response = dispatcher.BuildResponse(true, "ok", data, handler, false);

            Assert.AreEqual("json", response.format);

            for (var i = 0; i < 12; i++)
            {
                StringAssert.Contains($"\"name\":\"level{i}\"", response.data);
            }

            StringAssert.Contains(
                "\"name\":\"level10\",\"type\":\"Level10\",\"defaultValue\":\"\",\"children\":[{\"name\":\"level11\",\"type\":\"Level11\",\"defaultValue\":\"\",\"children\":[]}]",
                response.data);
            Assert.AreEqual(12, CountOccurrences(response.data, "\"children\":"));
        }

        private static CommandFieldInfo CreateFieldChain(int depth)
        {
            CommandFieldInfo current = null;

            for (var i = depth - 1; i >= 0; i--)
            {
                current = new CommandFieldInfo
                {
                    name = $"level{i}",
                    type = $"Level{i}",
                    defaultValue = "",
                    children = current == null
                        ? Array.Empty<CommandFieldInfo>()
                        : new[] { current }
                };
            }

            return current;
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var startIndex = 0;

            while ((startIndex = text.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
            {
                count++;
                startIndex += value.Length;
            }

            return count;
        }
    }
}
