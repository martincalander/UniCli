using System.Threading;
using NUnit.Framework;
using UniCli.Protocol;
using UniCli.Server.Editor.Handlers;
using UnityEditor;
using UnityEngine;

namespace UniCli.Server.Editor.Tests
{
    [TestFixture]
    public class ProjectInfoAndEditorQuitTests
    {
        [Test]
        public void ProjectInspect_IncludesCurrentBatchMode()
        {
            var context = new ServerContext("unicli-test");
            var handler = new ProjectInfoHandler(context);
            var request = new CommandRequest
            {
                command = "Project.Inspect",
                clientVersion = "1.2.0",
                cwd = "",
                data = ""
            };

            var response = ((ICommandHandler)handler)
                .ExecuteAsync(request, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            Assert.That(response, Is.TypeOf<ProjectInfoResponse>());
            Assert.AreEqual(Application.isBatchMode, ((ProjectInfoResponse)response).isBatchMode);
        }

        [Test]
        public void EditorQuit_RejectsInteractiveMode()
        {
            var exception = Assert.Throws<System.InvalidOperationException>(
                () => EditorQuitHandler.EnsureHeadlessMode(isBatchMode: false));

            Assert.AreEqual("Editor.Quit is only available in headless mode.", exception.Message);
        }

        [Test]
        public void EditorQuit_SchedulesExitAfterResponseFlushes()
        {
            EditorApplication.CallbackFunction scheduled = null;
            EditorApplication.CallbackFunction unregistered = null;
            var exitCode = -1;

            EditorQuitHandler.ScheduleQuit(
                registerDelayCall: action => scheduled = action,
                unregisterDelayCall: action => unregistered = action,
                exitEditor: code => exitCode = code);

            Assert.NotNull(scheduled);
            Assert.IsNull(unregistered);
            Assert.AreEqual(-1, exitCode);

            scheduled!();

            Assert.NotNull(unregistered);
            Assert.AreEqual(0, exitCode);
        }
    }
}
