using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace AiSign.Net
{
    internal sealed class ApiResponse
    {
        public int Status;
        public string Body;
        public string Error;
        public Dictionary<string, string> Headers = new Dictionary<string, string>();

        public bool Ok { get { return Status >= 200 && Status < 300; } }
        public bool NetworkError { get { return Status == 0; } }
    }

    internal static class HttpApi
    {
        static HttpApi()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            }
            catch { }
        }

        public static ApiResponse Get(string url, Dictionary<string, string> headers, int timeoutSec)
        {
            return Request("GET", url, headers, null, timeoutSec);
        }

        public static ApiResponse Post(string url, Dictionary<string, string> headers, string body, int timeoutSec)
        {
            return Request("POST", url, headers, body, timeoutSec);
        }

        public static ApiResponse Request(string method, string url, Dictionary<string, string> headers, string body, int timeoutSec)
        {
            ApiResponse result = new ApiResponse();
            HttpWebRequest req = null;
            try
            {
                req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = method;
                req.Timeout = Math.Max(5, timeoutSec) * 1000;
                req.ReadWriteTimeout = Math.Max(5, timeoutSec) * 1000;
                req.UserAgent = "AiSign/1.0";
                req.Accept = "application/json, text/plain, */*";
                req.KeepAlive = true;
                req.AllowAutoRedirect = true;

                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> kv in headers)
                    {
                        if (string.IsNullOrEmpty(kv.Key)) continue;
                        SetHeader(req, kv.Key, kv.Value);
                    }
                }

                if (body != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(body);
                    req.ContentType = "application/json; charset=utf-8";
                    req.ContentLength = data.Length;
                    using (Stream s = req.GetRequestStream())
                    {
                        s.Write(data, 0, data.Length);
                    }
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    result.Status = (int)resp.StatusCode;
                    result.Body = ReadBody(resp);
                    FillHeaders(result, resp);
                }
                return result;
            }
            catch (WebException ex)
            {
                HttpWebResponse resp = ex.Response as HttpWebResponse;
                if (resp != null)
                {
                    try
                    {
                        result.Status = (int)resp.StatusCode;
                        result.Body = ReadBody(resp);
                        FillHeaders(result, resp);
                    }
                    catch { }
                    resp.Close();
                }
                else
                {
                    result.Status = 0;
                    result.Error = ex.Message;
                }
                return result;
            }
            catch (Exception ex)
            {
                result.Status = 0;
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                if (req != null) req.Abort();
            }
        }

        private static void SetHeader(HttpWebRequest req, string key, string value)
        {
            if (value == null) return;
            string k = key.Trim();
            if (string.Equals(k, "User-Agent", StringComparison.OrdinalIgnoreCase)) { req.UserAgent = value; return; }
            if (string.Equals(k, "Accept", StringComparison.OrdinalIgnoreCase)) { req.Accept = value; return; }
            if (string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase)) { req.ContentType = value; return; }
            if (string.Equals(k, "Referer", StringComparison.OrdinalIgnoreCase)) { req.Referer = value; return; }
            if (string.Equals(k, "Host", StringComparison.OrdinalIgnoreCase)) { req.Host = value; return; }
            if (string.Equals(k, "Connection", StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(k, "Content-Length", StringComparison.OrdinalIgnoreCase)) return;
            req.Headers[k] = value;
        }

        private static string ReadBody(HttpWebResponse resp)
        {
            using (Stream s = resp.GetResponseStream())
            {
                if (s == null) return string.Empty;
                using (StreamReader reader = new StreamReader(s, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static void FillHeaders(ApiResponse result, HttpWebResponse resp)
        {
            try
            {
                for (int i = 0; i < resp.Headers.AllKeys.Length; i++)
                {
                    string k = resp.Headers.AllKeys[i];
                    result.Headers[k] = resp.Headers[k];
                }
            }
            catch { }
        }
    }
}
