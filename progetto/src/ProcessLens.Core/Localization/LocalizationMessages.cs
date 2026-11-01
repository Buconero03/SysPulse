using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ProcessLens.Core;
public static partial class SysPulseLocalization
{
    private sealed record CanonicalOrigin(string Text);
    private static readonly ConditionalWeakTable<string,CanonicalOrigin> Origins=new();
    private static readonly ConditionalWeakTable<string,Message> Messages=new();
    private static readonly ConditionalWeakTable<StringBuilder,BuilderParts> Builders=new();
    private static Dictionary<string,string> LoadCatalog() {
        using var stream=typeof(SysPulseLocalization).Assembly.GetManifestResourceStream("SysPulse.Analysis.en.json")??throw new InvalidDataException("Missing embedded analysis dictionary.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidDataException("Invalid analysis dictionary.");
    }
    public static JsonSerializerOptions ReportJsonOptions(bool indented=false) {
        var options=new JsonSerializerOptions{WriteIndented=indented};options.Converters.Add(new ReportStringConverter());return options;
    }
    private sealed class ReportStringConverter:System.Text.Json.Serialization.JsonConverter<string> {
        public override void WriteAsPropertyName(Utf8JsonWriter writer,string value,JsonSerializerOptions options)=>writer.WritePropertyName(value);
        public override string ReadAsPropertyName(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>reader.GetString()!;
        public override string Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>reader.GetString()!;
        public override void Write(Utf8JsonWriter writer,string value,JsonSerializerOptions options)=>writer.WriteStringValue(Render(value));
    }
    public static int CatalogCount=>Dictionary.Count;
    public static bool HasTranslation(string text)=>Dictionary.ContainsKey(text);
    public static string Literal(string text) {
        if(text.Length>0 && Dictionary.ContainsKey(text))Messages.GetValue(text,_=>new LiteralMessage(text));return text;
    }
    public static string KeyFor(string value)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
    private static readonly Lazy<Dictionary<string,string>> Keys=new(()=>Dictionary!.Keys.ToDictionary(KeyFor,s=>s));
    public static string ByKey(string key)=>Literal(Keys.Value.TryGetValue(key,out var value)?value:throw new KeyNotFoundException(key));
    private abstract record Message {public abstract string EnglishText(int depth);}
    private sealed record RawMessage(string Text):Message {public override string EnglishText(int depth)=>Text;}
    private sealed record LiteralMessage(string Text):Message {public override string EnglishText(int depth)=>Dictionary.GetValueOrDefault(Text,Text);}
    private sealed record PartsMessage(Message[] Parts):Message {public override string EnglishText(int depth)=>string.Concat(Parts.Select(p=>p.EnglishText(depth+1)));}
    private sealed record FormatMessage(string Format,object?[] Arguments):Message {
        public override string EnglishText(int depth) {
            string format=Regex.Replace(Format,@"(?<literal>(?:[^{}]|\{\{|\}\})+)|(?<hole>\{\d+(?:,-?\d+)?(?::[^{}]+)?\})",m=>m.Groups["hole"].Success?m.Value:Dictionary.GetValueOrDefault(m.Value,m.Value));
            return string.Format(CultureInfo.GetCultureInfo("en-US"),format,Arguments.Select(a=>a is Message msg?msg.EnglishText(depth+1):a).ToArray());
        }
    }
    private sealed record MappedMessage(Message Original,Func<string,string> Map):Message {public override string EnglishText(int depth)=>Map(Original.EnglishText(depth+1));}
    private sealed class BuilderParts {public readonly List<Message> Parts=new();}
    private sealed record OpaqueValue(object? Value):IFormattable {
        public string ToString(string? format,IFormatProvider? provider)=>Value is IFormattable f?f.ToString(format,provider):Value?.ToString()??"";
        public override string ToString()=>ToString(null,CultureInfo.CurrentCulture);
    }
    public static string? Raw(string? value)=>value is null?null:Track(value,new RawMessage(value));
    public static object Opaque(object? value)=>new OpaqueValue(value);
    private static Message Part(object? value) {
        if(value is OpaqueValue o)return new RawMessage(o.ToString());
        if(value is string s && Messages.TryGetValue(s,out var message))return message;
        string text=value?.ToString()??"";
        if(value is string constant && Dictionary.ContainsKey(constant))return new LiteralMessage(constant);
        return new RawMessage(text);
    }
    private static string Track(string value,Message message) {
        if(value.Length==0)return value;
        string copy=new(value.AsSpan());Messages.Add(copy,message);return copy;
    }
    public static string Capture(FormattableString message) {
        var values=message.GetArguments().Select(a=>a is OpaqueValue?a:a is string?(object)Part(a):a).ToArray();
        return Track(message.ToString(CultureInfo.CurrentCulture),new FormatMessage(message.Format,values));
    }
    public static string Concat(params object?[] parts)=>Track(string.Concat(parts.Select(p=>p?.ToString())),new PartsMessage(parts.Select(Part).ToArray()));
    public static string Join<T>(string? separator,IEnumerable<T> values) {
        var items=values.ToArray();var nodes=new List<Message>();
        for(int i=0;i<items.Length;i++){if(i>0)nodes.Add(Part(separator));nodes.Add(Part(items[i]));}
        return Track(string.Join(separator,items),new PartsMessage(nodes.ToArray()));
    }
    public static string Join<T>(char separator,IEnumerable<T> values)=>Join(separator.ToString(),values);
    public static StringBuilder Append(StringBuilder builder,object? value) {builder.Append(value);Builders.GetValue(builder,_=>new BuilderParts()).Parts.Add(Part(value));return builder;}
    public static StringBuilder AppendLine(StringBuilder builder,string? value=null) {builder.AppendLine(value);var p=Builders.GetValue(builder,_=>new BuilderParts()).Parts;p.Add(Part(value));p.Add(new RawMessage(Environment.NewLine));return builder;}
    public static string BuilderText(StringBuilder builder)=>Builders.TryGetValue(builder,out var parts)?Track(builder.ToString(),new PartsMessage(parts.Parts.ToArray())):builder.ToString();
    public static string Map(string? original,Func<string,string?> map) {string? result=map(original!);if(original is null||result is null)return result!;return Messages.TryGetValue(original,out var source)?Track(result,new MappedMessage(source,s=>map(s)??"")):result;}
    public static string Render(string text,AppLanguage? language=null) {
        if((language??CurrentLanguage)==AppLanguage.Italian)return Origins.TryGetValue(text,out var original)?original.Text:text;
        if(!Messages.TryGetValue(text,out var message))return text;
        string translated=message.EnglishText(0);if(translated==text)return text;
        string copy=new(translated.AsSpan());Origins.Add(copy,new CanonicalOrigin(text));return copy;
    }
}
