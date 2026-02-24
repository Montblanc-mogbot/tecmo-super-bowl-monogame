using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Content;
using TecmoSB.Content;

namespace TecmoSBGame;

/// <summary>
/// MonoGame ContentManager wrapper that provides unified access to YAML content.
/// </summary>
public sealed class TecmoContentManager : IDisposable
{
    private readonly ContentManager _mgContent;
    private readonly YamlContentLoader _yamlLoader;
    private readonly string _yamlContentRoot;

    public YamlContentLoader YamlLoader => _yamlLoader;

    public TecmoContentManager(IServiceProvider serviceProvider, string rootDirectory, string yamlContentRoot = "content")
    {
        _mgContent = new ContentManager(serviceProvider, rootDirectory);
        _yamlLoader = new YamlContentLoader();
        _yamlContentRoot = yamlContentRoot;
    }

    /// <summary>
    /// Loads MonoGame content (textures, sounds, etc.).
    /// </summary>
    public T Load<T>(string assetName)
    {
        return _mgContent.Load<T>(assetName);
    }

    /// <summary>
    /// Loads YAML content by path relative to the YAML content root.
    /// </summary>
    public T LoadYaml<T>(string yamlPath)
    {
        var fullPath = Path.Combine(_yamlContentRoot, yamlPath);
        return _yamlLoader.Load<T>(fullPath);
    }

    /// <summary>
    /// Tries to load YAML content. Returns null if loading fails.
    /// </summary>
    public T? TryLoadYaml<T>(string yamlPath)
    {
        var fullPath = Path.Combine(_yamlContentRoot, yamlPath);
        return _yamlLoader.TryLoad<T>(fullPath);
    }

    /// <summary>
    /// Loads all YAML files from a directory that match a pattern.
    /// </summary>
    public List<T> LoadYamlDirectory<T>(string directory, string searchPattern = "*.yaml")
    {
        var results = new List<T>();
        var fullDir = Path.Combine(_yamlContentRoot, directory);

        if (!Directory.Exists(fullDir))
        {
            return results;
        }

        foreach (var file in Directory.EnumerateFiles(fullDir, searchPattern))
        {
            try
            {
                var content = _yamlLoader.Load<T>(file);
                if (content != null)
                {
                    results.Add(content);
                }
            }
            catch (YamlContentLoadException ex)
            {
                // Log error but continue loading other files
                Console.WriteLine($"[Content] Failed to load {file}: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    /// Gets all content file paths in a directory.
    /// </summary>
    public IEnumerable<string> GetContentPaths(string directory, string searchPattern = "*.yaml")
    {
        var fullDir = Path.Combine(_yamlContentRoot, directory);
        if (!Directory.Exists(fullDir))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateFiles(fullDir, searchPattern)
            .Select(f => Path.GetRelativePath(_yamlContentRoot, f));
    }

    /// <summary>
    /// Unloads all content.
    /// </summary>
    public void Unload()
    {
        _mgContent.Unload();
        _yamlLoader.ClearAllCache();
    }

    public void Dispose()
    {
        Unload();
        _mgContent.Dispose();
    }
}
