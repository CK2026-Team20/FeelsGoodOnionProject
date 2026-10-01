using System;
using System.Collections;
using System.Threading;
using Cooked.Contracts;
using Cooked.Foundation;
using UnityEngine;

namespace Cooked.Integration.Editor
{
    /// <summary>Destructive validation of the current REAL Core session. Run only as an explicit failure scenario,
    /// then request ReturnToTitle through the normal flow. Not executed by import or on startup.</summary>
    public static class IntegrationNativeShutdownVerification
    {
        public static IEnumerator Run(GameRuntimeService runtime, Action<string> report)
        {
            if (!Application.isPlaying || runtime?.CurrentSession == null)
                throw new InvalidOperationException("Native teardown test requires a real active Play Mode session.");
            if (report == null) throw new ArgumentNullException(nameof(report));
            var old = runtime.CurrentSession;
            if (!old.Player.HasActor) throw new InvalidOperationException("Spawn the real Actor before this failure test.");
            var injected = new InvalidOperationException("Injected native ActorRemoving failure");
            int calls = 0, laterSubscriberCalls = 0;
            void FailRemoving() { calls++; throw injected; }
            void ObserveLaterCleanup() { laterSubscriberCalls++; }
            old.ActorRemoving += FailRemoving;
            old.ActorRemoving += ObserveLaterCleanup;
            try
            {
                var first = new OperationResult(); Exception failure = null;
                yield return CoroutineOperationRunner.Run(runtime.ShutdownSession(CancellationToken.None, first), first, e => failure = e);
                if (first.Status != OperationStatus.Failed || !Contains(failure, injected))
                    throw new InvalidOperationException("Original ActorRemoving exception was lost.");
                if (runtime.CurrentSession != null || old.Session.IsActive || old.Player.HasActor || old.Dialogue.IsActive)
                    throw new InvalidOperationException("Native owner/Actor/Session/Dialogue remained after failed teardown.");
                if (laterSubscriberCalls != calls || laterSubscriberCalls == 0)
                    throw new InvalidOperationException("One failing removal listener prevented later cleanup.");
                int beforeRecovery = calls;
                var recovery = new OperationResult();
                yield return CoroutineOperationRunner.Run(runtime.ShutdownSession(CancellationToken.None, recovery), recovery);
                if (recovery.Status != OperationStatus.Succeeded || calls != beforeRecovery)
                    throw new InvalidOperationException("Recovery reused the failed old session.");
                report("PASS native ShutdownSession: original failure retained, owner/Actor/session cleared, repeated recovery succeeded. ReturnToTitle is still required.");
            }
            finally { old.ActorRemoving -= FailRemoving; old.ActorRemoving -= ObserveLaterCleanup; }
        }
        private static bool Contains(Exception error, Exception expected)
        {
            if (ReferenceEquals(error, expected)) return true;
            if (error is AggregateException multiple)
                foreach (var child in multiple.InnerExceptions) if (Contains(child, expected)) return true;
            return error?.InnerException != null && Contains(error.InnerException, expected);
        }
    }
}

