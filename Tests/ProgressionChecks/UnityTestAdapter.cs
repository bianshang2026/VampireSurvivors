// Minimal host adapter for testing production economy/save code without the Unity runtime.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace UnityEngine
{
    public class MonoBehaviour { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string title) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public struct Color { public Color(float r, float g, float b, float a = 1) { } }
    public static class Random { public static float value => .5f; }
    public static class Application { public static string persistentDataPath = Path.Combine(Directory.GetCurrentDirectory(), "Tests/ProgressionChecks/bin/cultivation-check-" + Guid.NewGuid().ToString("N")); }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Clamp(int v, int min, int max) => Math.Clamp(v, min, max);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
    }
    public static class Debug
    {
        public static void Log(string message) { }
        public static void LogWarning(string message) { }
        public static void LogError(string message) { throw new Exception(message); }
    }
    public static class JsonUtility
    {
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { IncludeFields = true });
        public static string ToJson(object data, bool pretty) => JsonSerializer.Serialize(data, new JsonSerializerOptions { IncludeFields = true, WriteIndented = pretty });
    }
}
namespace BiopunkRV
{
    public enum Element { None, Water, Fire, Wind, Earth, Thunder, Metal, FlameWind, Frost, Wood, MetalFusion }
    public class CultivationMethod { public string baseSkillId; }
    public static class MethodEditor
    {
        public static CultivationMethod ResolveById(string id) => id == "orb" || id == "orb2" ? new CultivationMethod { baseSkillId = "SpiritOrb" } : id == "sword" ? new CultivationMethod { baseSkillId = "FlyingSword" } : null;
    }
    public static class RunInventory
    {
        public static int spiritStones, methodFragments, affixFragments;
        public static void Reset() { spiritStones = methodFragments = affixFragments = 0; }
    }
}
