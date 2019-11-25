using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Acklann.Tees
{
    public class TypescriptCompiler
    {
        public static CompilerResult Compile(string documentPath, CompilerOptions options)
        {
            if (!File.Exists(documentPath)) throw new FileNotFoundException($"Could not find file at '{documentPath}'.");

            string compiler = Path.Combine(NodeJS.InstallationDirectory, "compiler.js");
            if (!File.Exists(compiler)) throw new FileNotFoundException($"Could not find file at '{compiler}'.");

            if (!string.IsNullOrEmpty(options.OutputDirectory) && !Directory.Exists(options.OutputDirectory))
                Directory.CreateDirectory(options.OutputDirectory);

            if (!string.IsNullOrEmpty(options.SourceMapDirectory) && !Directory.Exists(options.SourceMapDirectory))
                Directory.CreateDirectory(options.SourceMapDirectory);

            long start = System.DateTime.Now.Ticks;
            using (Process node = NodeJS.Execute($"/c node \"{compiler}\" \"{documentPath}\" {options.ToArgs()}"))
            {
                return new CompilerResult
                {
                    SourceFile = documentPath,
                    Success = (node.ExitCode == 0),
                    Errors = GetErrors(node.StandardError).ToArray(),
                    GeneratedFiles = GetGeneratedFiles(node.StandardOutput).ToArray(),
                    Elapse = System.TimeSpan.FromTicks(System.DateTime.Now.Ticks - start)
                };
            }
        }

        public static IEnumerable<string> FindFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) throw new DirectoryNotFoundException($"Could not find directory at '{directoryPath}'.");

            return from x in Directory.EnumerateFiles(directoryPath, "*.ts", SearchOption.AllDirectories)
                   where !x.Contains(@"\node_modules\")
                   select x;
        }

        private static IEnumerable<string> GetGeneratedFiles(StreamReader reader)
        {
            JArray json; string line = null;

            while (!reader.EndOfStream)
            {
                line = reader.ReadLine();
#if DEBUG
                System.Diagnostics.Debug.WriteLine(line);
#endif
                if (string.IsNullOrEmpty(line) || !line.StartsWith("[")) continue;

                json = JArray.Parse(line);
                return json.Values<string>();
            }

            return new string[0];
        }

        private static IEnumerable<CompilerError> GetErrors(StreamReader reader)
        {
            if (reader == null) yield break;

            JObject json; string line = null;
            while (!reader.EndOfStream)
            {
                line = reader.ReadLine();
#if DEBUG
                System.Diagnostics.Debug.WriteLine(line);
#endif
                if (string.IsNullOrEmpty(line) || !line.StartsWith("{")) continue;

                json = JObject.Parse(line);
                yield return new CompilerError(
                    json["message"].Value<string>(),
                    (json["file"]?.Value<string>() ?? default),
                    (json["line"]?.Value<int>() ?? default),
                    (json["column"]?.Value<int>() ?? default),
                    ((ErrorSeverity)(json["level"]?.Value<int>() ?? (int)ErrorSeverity.Error)),
                    (json["status"]?.Value<int>() ?? default)
                );
            }
        }
    }
}