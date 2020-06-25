using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Acklann.TSBuild.CodeGeneration.Generators
{
	internal class CodeWriter : StreamWriter
	{
		public CodeWriter(Stream stream, Encoding encoding, TypescriptGeneratorSettings settings) : base(stream, encoding)
		{
			_settings = settings;
		}

		public void WriteReferencePaths()
		{
			if (_settings.References != null && _settings.References.Length > 0)
			{
				foreach (string refPath in _settings.References)
					if (!string.IsNullOrEmpty(refPath))
					{
						WriteLine($"/// <reference path=\"{refPath}\" />");
					}
				WriteLine();
			}
		}

		public void WriteNamespaceStart()
		{
			if (_settings.HasNamespace)
			{
				WriteLine($"namespace {_settings.Namespace} {{");
				PushIndent();
			}
		}

		public void WriteNamespaceEnd()
		{
			if (_settings.HasNamespace)
			{
				WriteLine('}');
				PopIndent();
			}
		}

		public void WriteProperty(MemberDefinition member, bool optional = false, bool knockout = false)
		{
			Write(GetIndent());
			Write(member.Name.ToCamel());
			if (optional) Write('?');
			Write(": ");
			Write(GetDataType(member.Type, _settings.Prefix, _settings.Suffix, knockout));
			WriteLine(';');
		}

		public void WriteEnumValue(MemberDefinition member)
		{
			Write(GetIndent());
			Write(member.Name);
			if (member.DefaultValue != null) Write($" = {member.DefaultValue}");
		}

		internal static string GetDataType(TypeDefinition definition, string prefix, string suffix, bool knockout = false)
		{
			bool isPrimitive = true;

			string name = definition.Name;
			if (definition.IsCollection && definition.ParameterList?.Count == 1) name = definition.ParameterList[0].Name;

			switch (name)
			{
				case "char":
				case "string":
				case nameof(Char):
				case nameof(String):
					name = "string";
					break;

				case "int":
				case "long":
				case "byte":
				case "float":
				case "sbyte":
				case "double":
				case "single":
				case "numeric":
				case "decimal":
				case nameof(Int16):
				case nameof(Int64):
				case nameof(Int32):
				case nameof(Double):
				case nameof(Single):
				case nameof(SByte):
					name = "number";
					break;

				case "bool":
				case nameof(Boolean):
					name = "boolean";
					break;

				default:
					name = (definition.InScope ? NormalizeName(definition, prefix, suffix) : "any");
					isPrimitive = false;
					break;
			}

			if (knockout)
			{
				if (isPrimitive || definition.IsEnum)
					return (definition.IsCollection ? $"KnockoutObservableArray<{name}>" : $"KnockoutObservable<{name}>");
				else
					return (definition.IsCollection ? $"KnockoutObservableArray<{name}>" : name);
			}
			else return (definition.IsCollection ? $"Array<{name}>" : name);
		}

		public void WriteTypeSignature(TypeDefinition definition)
		{
			Write(NormalizeName(definition, _settings));
			if (definition.HasParameters)
			{
				Write('<');

				TypeDefinition type;
				int n = definition.ParameterList.Count;
				for (int i = 0; i < n; i++)
				{
					type = definition.ParameterList[i];

					if (type.InScope) Write(NormalizeName(type, _settings));
					else Write("any");

					if (i < (n - 1)) Write(", ");
				}
				Write('>');
			}
		}

		public void WriteTypeBaseList(TypeDefinition definition)
		{
			if (definition?.BaseList?.Count > 0)
			{
				bool onFirstItem = true;
				foreach (TypeDefinition def in definition.BaseList.Where(x => x.IsClass))
				{
					if (onFirstItem)
					{
						Write(" extends ");
						onFirstItem = false;
					}
					else Write(", ");
					WriteTypeSignature(def);
				}

				onFirstItem = true;
				foreach (TypeDefinition def in definition.BaseList.Where(x => x.IsInterface))
				{
					if (onFirstItem)
					{
						if (definition.IsClass) Write(" implements ");
						else Write(" extends ");
						onFirstItem = false;
					}
					else Write(", ");
					WriteTypeSignature(def);
				}
			}
		}

		public void WriteIndent(string content = default)
		{
			Write(string.Concat(GetIndent(), content));
		}

		public void CloseBrace()
		{
			PopIndent();
			WriteLine(string.Concat(GetIndent(), '}'));
		}

		public void EmitLine(string content)
		{
			base.WriteLine(string.Concat(GetIndent(), content));
		}

		public void PushIndent()
		{
			_depth++;
		}

		public void PopIndent()
		{
			_depth--;
		}

		#region Backing Members

		private int _depth;
		private readonly TypescriptGeneratorSettings _settings;

		private static string NormalizeName(TypeDefinition definition, TypescriptGeneratorSettings settings)
		{
			string prefix = (!definition.IsEnum && !string.IsNullOrEmpty(settings.Prefix) ? settings.Prefix : string.Empty);
			string suffix = (!definition.IsEnum && !string.IsNullOrEmpty(settings.Suffix) ? settings.Suffix : string.Empty);
			return NormalizeName(definition, prefix, suffix);
		}

		private static string NormalizeName(TypeDefinition definition, string prefix, string suffix)
		{
			prefix = (!definition.IsEnum && !string.IsNullOrEmpty(prefix) ? prefix : string.Empty);
			suffix = (!definition.IsEnum && !string.IsNullOrEmpty(suffix) ? suffix : string.Empty);
			return string.Concat(prefix, definition.Name.ToPascal(), suffix).Trim();
		}

		private string GetIndent()
		{
			if (_depth <= 0) return string.Empty;
			else return string.Concat(Enumerable.Repeat('\t', _depth)) ?? string.Empty;
		}

		#endregion Backing Members
	}
}