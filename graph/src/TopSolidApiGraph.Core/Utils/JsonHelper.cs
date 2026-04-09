using System;
using System.IO;
using Newtonsoft.Json;

namespace TopSolidApiGraph.Core.Utils
{
    /// <summary>
    /// Utility class for JSON serialization and deserialization using Newtonsoft.Json.
    /// </summary>
    public static class JsonHelper
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        /// <summary>
        /// Saves an object to a JSON file.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="data">The object to save.</param>
        /// <param name="filePath">The path to the output file.</param>
        public static void Save<T>(T data, string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            string json = JsonConvert.SerializeObject(data, Settings);
            File.WriteAllText(filePath, json);
            Console.WriteLine($"[JsonHelper] Saved data to {filePath}");
        }

        /// <summary>
        /// Loads an object from a JSON file.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="filePath">The path to the JSON file.</param>
        /// <returns>The deserialized object, or default(T) if the file doesn't exist.</returns>
        public static T Load<T>(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[JsonHelper] File not found: {filePath}");
                return default(T);
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }
    }
}
