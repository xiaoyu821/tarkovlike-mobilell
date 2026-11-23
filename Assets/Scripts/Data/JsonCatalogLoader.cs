using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Game.Data
{
    /// <summary>数据文件信封：{ "schemaVersion": 1, "entries": [...] }。</summary>
    public sealed class DataFile<T>
    {
        public int SchemaVersion { get; set; }
        public List<T> Entries { get; set; } = new List<T>();
    }

    /// <summary>从 StreamingAssets/GameData 读取 JSON 数据表。</summary>
    public static class JsonCatalogLoader
    {
        public const int SupportedSchemaVersion = 1;

        public const string ItemsFileName = "items.json";
        public const string WeaponsFileName = "weapons.json";
        public const string AmmoFileName = "ammo.json";
        public const string ArmorFileName = "armor.json";
        public const string LootTablesFileName = "loot_tables.json";

        public static DataCatalog LoadFromDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("数据目录不能为空", nameof(directory));
            }

            var settings = CreateSettings();

            return DataCatalog.Create(
                ReadEntries<ItemData>(directory, ItemsFileName, settings),
                ReadEntries<WeaponData>(directory, WeaponsFileName, settings),
                ReadEntries<AmmoData>(directory, AmmoFileName, settings),
                ReadEntries<ArmorData>(directory, ArmorFileName, settings),
                ReadEntries<LootTableData>(directory, LootTablesFileName, settings));
        }

        /// <summary>加载并校验，任何问题直接抛 DataValidationException。</summary>
        public static DataCatalog LoadFromDirectoryOrThrow(string directory)
        {
            var catalog = LoadFromDirectory(directory);
            catalog.ThrowIfInvalid();
            return catalog;
        }

        private static List<T> ReadEntries<T>(string directory, string fileName, JsonSerializerSettings settings)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("找不到数据文件: " + path, path);
            }

            var file = JsonConvert.DeserializeObject<DataFile<T>>(File.ReadAllText(path), settings);
            if (file == null)
            {
                throw new InvalidDataException("数据文件为空或格式错误: " + path);
            }

            if (file.SchemaVersion != SupportedSchemaVersion)
            {
                throw new InvalidDataException(
                    "数据文件 " + fileName + " 的 schemaVersion=" + file.SchemaVersion +
                    "，当前仅支持 " + SupportedSchemaVersion);
            }

            return file.Entries ?? new List<T>();
        }

        private static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() },
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DateParseHandling = DateParseHandling.None
            };
        }
    }
}
