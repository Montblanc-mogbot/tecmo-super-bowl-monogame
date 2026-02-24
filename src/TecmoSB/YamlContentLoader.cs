using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB.Content;

/// <summary>
/// Generic YAML content loader with caching and error handling.
/// </summary>
public sealed class YamlContentLoader
{
    private readonly IDeserializer _deserializer;
    private readonly Dictionary<Type, Dictionary<string, object>> _cache = new();

    public YamlContentLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Loads YAML content from a file and deserializes it to type T.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="path">Path to the YAML file.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="FileNotFoundException">If the file doesn't exist.</exception>
    /// <exception cref="YamlContentLoadException">If deserialization fails.</exception>
    public T Load<T>(string path)
    {
        var type = typeof(T);
        var key = Path.GetFullPath(path);

        // Check cache first
        if (_cache.TryGetValue(type, out var typeCache) && typeCache.TryGetValue(key, out var cached))
        {
            return (T)cached;
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"YAML content file not found: {path}", path);
        }

        try
        {
            var yaml = File.ReadAllText(path);
            var result = _deserializer.Deserialize<T>(yaml);

            if (result == null)
            {
                throw new YamlContentLoadException($"Deserialization returned null for {path}");
            }

            // Cache the result
            if (!_cache.TryGetValue(type, out typeCache))
            {
                typeCache = new Dictionary<string, object>();
                _cache[type] = typeCache;
            }
            typeCache[key] = result;

            return result;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new YamlContentLoadException($"Failed to parse YAML in {path}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not YamlContentLoadException)
        {
            throw new YamlContentLoadException($"Failed to load YAML content from {path}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Tries to load YAML content from a file. Returns null if loading fails.
    /// </summary>
    public T? TryLoad<T>(string path)
    {
        try
        {
            return Load<T>(path);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Clears the content cache for a specific type, or all types if null.
    /// </summary>
    public void ClearCache<T>()
    {
        var type = typeof(T);
        if (_cache.ContainsKey(type))
        {
            _cache.Remove(type);
        }
    }

    /// <summary>
    /// Clears all cached content.
    /// </summary>
    public void ClearAllCache()
    {
        _cache.Clear();
    }
}

/// <summary>
/// Exception thrown when YAML content loading fails.
/// </summary>
public class YamlContentLoadException : Exception
{
    public YamlContentLoadException(string message) : base(message) { }
    public YamlContentLoadException(string message, Exception inner) : base(message, inner) { }
}
