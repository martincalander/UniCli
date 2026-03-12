using System;
using System.Threading;
using System.Threading.Tasks;
using UniCli.Protocol;
using UnityEditor;
using UnityEngine;

namespace UniCli.Server.Editor.Handlers
{
    public sealed class EditorQuitHandler : CommandHandler<Unit, Unit>
    {
        public override string CommandName => "Editor.Quit";
        public override string Description => "Quit the headless Unity Editor process gracefully via EditorApplication.Exit";

        protected override ValueTask<Unit> ExecuteAsync(Unit request, CancellationToken cancellationToken)
        {
            EnsureHeadlessMode();
            ScheduleQuit(
                action => EditorApplication.delayCall += action,
                action => EditorApplication.delayCall -= action,
                EditorApplication.Exit);
            return new ValueTask<Unit>(Unit.Value);
        }

        internal static void EnsureHeadlessMode()
        {
            EnsureHeadlessMode(Application.isBatchMode);
        }

        internal static void EnsureHeadlessMode(bool isBatchMode)
        {
            if (!isBatchMode)
                throw new InvalidOperationException("Editor.Quit is only available in headless mode.");
        }

        internal static void ScheduleQuit(
            Action<EditorApplication.CallbackFunction> registerDelayCall,
            Action<EditorApplication.CallbackFunction> unregisterDelayCall,
            Action<int> exitEditor)
        {
            void Exit()
            {
                unregisterDelayCall(Exit);
                exitEditor(0);
            }

            registerDelayCall(Exit);
        }
    }
}
