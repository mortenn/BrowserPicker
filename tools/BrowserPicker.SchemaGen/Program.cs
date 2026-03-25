using System.Text.Json;
using BrowserPicker.Common;
using NJsonSchema;
using NJsonSchema.Generation;

var outputPath = args.Length > 0
	? Path.GetFullPath(args[0])
	: Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "schemas", "browserpicker-settings.schema.json"));

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var generatorSettings = new SystemTextJsonSchemaGeneratorSettings
{
	SchemaType = SchemaType.JsonSchema,
	SerializerOptions = new JsonSerializerOptions()
};

var schema = JsonSchema.FromType<SerializableSettings>(generatorSettings);
schema.Title = "Browser Picker settings";
schema.Description = "Settings document used by Browser Picker for backups, clipboard import/export, and persisted JSON settings.";
schema.Id = SerializableSettings.JsonSchemaUrl;
schema.AllowAdditionalProperties = true;

var schemaJson = schema.ToJson();
File.WriteAllText(outputPath, schemaJson + Environment.NewLine);

Console.WriteLine($"Wrote schema to {outputPath}");
