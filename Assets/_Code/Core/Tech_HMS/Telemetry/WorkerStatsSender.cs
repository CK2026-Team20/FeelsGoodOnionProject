using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Cooked.Telemetry
{
    public sealed class WorkerStatsSender : IDisposable
    {
        private UnityWebRequest activeRequest;
        public bool IsSending => activeRequest != null;

        [Serializable]
        private sealed class SaveResponse
        {
            public bool ok;
            public long userid;
            public string installation_id;
            public string session_id;
            public int jump_count;
            public string recorded_at;
        }

        public IEnumerator Send(string endpoint, PlayStatsData snapshot, int timeoutSeconds,
            Action<bool, string> completed)
        {
            if (IsSending)
            {
                completed?.Invoke(false, "이미 전송 중입니다.");
                yield break;
            }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            {
                completed?.Invoke(false, "올바른 HTTPS 통계 API 주소를 입력하세요.");
                yield break;
            }

            if (snapshot == null || !Guid.TryParseExact(snapshot.installation_id, "N", out _)
                || !Guid.TryParseExact(snapshot.session_id, "N", out _)
                || string.IsNullOrEmpty(snapshot.recorded_at)
                || snapshot.jump_count < 0)
            {
                completed?.Invoke(false, "통계 데이터가 올바르지 않습니다.");
                yield break;
            }

            bool success;
            string message;
            var request = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST);
            activeRequest = request;
            try
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Math.Max(1, timeoutSeconds);
                request.redirectLimit = 0;
                yield return request.SendWebRequest();
                success = request.result == UnityWebRequest.Result.Success;
                message = $"전송 실패 ({request.responseCode}): {request.error}";
                if (success)
                {
                    SaveResponse response = null;
                    try { response = JsonUtility.FromJson<SaveResponse>(request.downloadHandler.text); }
                    catch (ArgumentException) { }
                    success = response != null && response.ok && response.userid > 0
                        && response.installation_id == snapshot.installation_id
                        && response.session_id == snapshot.session_id
                        && response.jump_count == snapshot.jump_count
                        && response.recorded_at == snapshot.recorded_at;
                    message = success
                        ? $"D1 저장 확인: userid={response.userid}, 점프={response.jump_count}, 시각={response.recorded_at}"
                        : "서버의 저장 확인 응답이 전송 데이터와 일치하지 않습니다.";
                }
            }
            finally
            {
                if (ReferenceEquals(activeRequest, request))
                {
                    activeRequest = null;
                    request.Dispose();
                }
            }
            completed?.Invoke(success, message);
        }

        public void Dispose()
        {
            var request = activeRequest;
            activeRequest = null;
            if (request == null) return;
            request.Abort();
            request.Dispose();
        }
    }
}