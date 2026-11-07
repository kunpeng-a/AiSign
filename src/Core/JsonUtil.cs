using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace AiSign.Core
{
    internal static class JsonUtil
    {
        private static JavaScriptSerializer Serializer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = 20 * 1024 * 1024;
            s.RecursionLimit = 200;
            return s;
        }

        public static string Serialize(object value)
        {
            return Serializer().Serialize(value);
        }

        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default(T);
            return Serializer().Deserialize<T>(json);
        }

        public static Dictionary<string, object> ParseObject(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return null;
                return Serializer().Deserialize<Dictionary<string, object>>(json);
            }
            catch
            {
                return null;
            }
        }

        public static Dictionary<string, object> GetDict(Dictionary<string, object> d, string key)
        {
            if (d == null || key == null) return null;
            object v;
            if (d.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }

        public static ArrayList GetArray(Dictionary<string, object> d, string key)
        {
            if (d == null || key == null) return null;
            object v;
            if (d.TryGetValue(key, out v)) return v as ArrayList;
            return null;
        }

        public static string GetString(Dictionary<string, object> d, string key)
        {
            if (d == null || key == null) return null;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return null;
            return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static string GetStringAny(Dictionary<string, object> d, params string[] keys)
        {
            if (d == null || keys == null) return null;
            for (int i = 0; i < keys.Length; i++)
            {
                string s = GetString(d, keys[i]);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }

        public static int GetInt(Dictionary<string, object> d, string key)
        {
            return (int)GetLong(d, key);
        }

        public static int GetIntAny(Dictionary<string, object> d, params string[] keys)
        {
            if (d == null || keys == null) return 0;
            for (int i = 0; i < keys.Length; i++)
            {
                long v = GetLong(d, keys[i]);
                if (v != 0) return (int)v;
            }
            return 0;
        }

        public static long GetLong(Dictionary<string, object> d, string key)
        {
            if (d == null || key == null) return 0;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return 0;
            try
            {
                if (v is int) return (int)v;
                if (v is long) return (long)v;
                if (v is double) return (long)(double)v;
                if (v is decimal) return (long)(decimal)v;
                string s = Convert.ToString(v, CultureInfo.InvariantCulture);
                long n;
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
                double dbl;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out dbl)) return (long)dbl;
            }
            catch { }
            return 0;
        }

        public static bool GetBool(Dictionary<string, object> d, string key)
        {
            if (d == null || key == null) return false;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return false;
            if (v is bool) return (bool)v;
            string s = Convert.ToString(v, CultureInfo.InvariantCulture);
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
        }

        public static bool GetBoolAny(Dictionary<string, object> d, params string[] keys)
        {
            if (d == null || keys == null) return false;
            for (int i = 0; i < keys.Length; i++)
                if (GetBool(d, keys[i])) return true;
            return false;
        }

        public static int? GetNullableInt(Dictionary<string, object> d, string key)
        {
            if (d == null || key == null) return null;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return null;
            return (int)GetLong(d, key);
        }
    }
}
