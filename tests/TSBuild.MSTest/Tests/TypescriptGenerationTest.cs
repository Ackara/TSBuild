using Acklann.Diffa;
using Acklann.TSBuild.CodeGeneration;
using Acklann.TSBuild.CodeGeneration.Generators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Acklann.TSBuild.Tests
{
	[TestClass]
	public class TypescriptGenerationTest
	{
		[TestMethod]
		public void Can_emit_enum_definition_as_as_dts()
		{
			// Arrange
			var declaration = new TypeDefinition("Foo", (Trait.Public | Trait.Enum))
				.Add(new MemberDefinition("A", new TypeDefinition("int", (Trait.Enum | Trait.Public))))
				.Add(new MemberDefinition("B", new TypeDefinition("int", (Trait.Enum | Trait.Public))));

			// Act + Assert
			string ts = UTF8(DeclarationFileGenerator.Emit(declaration));
			ts.ShouldMatch(@"declare enum Foo\s+{\s+A,\s+B\s+}");

			var config = new TypescriptGeneratorSettings(ns: "Foo");
			ts = UTF8(DeclarationFileGenerator.Emit(config, declaration));
			ts.ShouldMatch(@"declare namespace Foo\s+{\s+enum Foo\s+{\s+A,\s+B\s+}\s+}");
		}

		[DataTestMethod]
		[ApprovedFolder("approved-results/spec-results/ts")]
		[DynamicData(nameof(GetSpecifications), DynamicDataSourceType.Method)]
		public void Can_emit_typescript_files(string[] sourceFiles, string label)
		{
			var config = new TypescriptGeneratorSettings("App");
			string results = UTF8(TypescriptGenerator.Emit(config, sourceFiles));
			Diff.Approve(results, ".ts", label);
		}

		[DataTestMethod]
		[ApprovedFolder("approved-results/spec-results/dts")]
		[DynamicData(nameof(GetSpecifications), DynamicDataSourceType.Method)]
		public void Can_emit_declaration_files(string[] sourceFiles, string label)
		{
			var config = new TypescriptGeneratorSettings();
			string results = UTF8(DeclarationFileGenerator.Emit(config, sourceFiles));
			Diff.Approve(results, ".ts", label);
		}

		[DataTestMethod]
		[ApprovedFolder("approved-results/spec-results/ko")]
		[DynamicData(nameof(GetSpecifications), DynamicDataSourceType.Method)]
		public void Can_emit_knockout_js_files(string[] sourceFiles, string label)
		{
			var config = new TypescriptGeneratorSettings("App", suffix: "Model", koJs: true, references: new string[] { "../../../../node_modules/@types/knockout/index.d.ts" });
			var results = KnockoutJsGenerator.Emit(config, sourceFiles);
			Diff.Approve(UTF8(results), ".ts", label);
		}

		#region Backing Members

		private static IEnumerable<object[]> GetDefinitions()
		{
			var status = new TypeDefinition("Status", (Trait.Public | Trait.Enum))
				.Add(new MemberDefinition("Striving", new TypeDefinition("int"), defaultValue: 5))
				.Add(new MemberDefinition("Endangered", new TypeDefinition("int")))
				.Add(new MemberDefinition("Instinct", new TypeDefinition("int")));

			var animal = new TypeDefinition("Animal", (Trait.Public | Trait.Interface))
				.Add(new MemberDefinition("Name", new TypeDefinition("string"), Trait.Public))
				.Add(new MemberDefinition("Legs", new TypeDefinition("int"), Trait.Public))
				.Add(new MemberDefinition("Status", status, Trait.Public));

			yield return new object[]
			{
				"simple",
				new TypeDefinition[] { animal }
			};

			// -----

			var feline = new TypeDefinition("Feline", (Trait.Public | Trait.Interface))
				.Inherit(animal)
				.Add(new MemberDefinition("Whiskers", new TypeDefinition("int"), Trait.Public));

			var pathera = new TypeDefinition("Pathera", (Trait.Public | Trait.Interface))
				.Inherit(animal)
				.Add(new MemberDefinition("Stealth", new TypeDefinition("int"), Trait.Public));

			var lion = new TypeDefinition("Lion", (Trait.Public | Trait.Class))
				.Inherit(feline)
				.Inherit(pathera)
				.Add(new MemberDefinition("Name", new TypeDefinition("string"), Trait.Public))
				.Add(new MemberDefinition("Legs", new TypeDefinition("int"), Trait.Public))
				.Add(new MemberDefinition("Status", status, Trait.Public))
				.Add(new MemberDefinition("Kills", new TypeDefinition("int"), Trait.Public))
				.Add(new MemberDefinition("Whiskers", new TypeDefinition("int"), Trait.Public))
				.Add(new MemberDefinition("Stealth", new TypeDefinition("int"), Trait.Public))
				;

			yield return new object[]
			{
				"inheritance",
				TypeDefinition.ResolveDependencies(new TypeDefinition[] { status, animal, feline, pathera, lion }).ToArray()
			};

			// -----

			var skill = new TypeDefinition("Skill", (Trait.Public | Trait.Interface))
				.Add(new TypeDefinition("T"));

			var predator = new TypeDefinition("Predator", (Trait.Public | Trait.Class))
				.Add(new MemberDefinition("Name", new TypeDefinition("string"), Trait.Public));

			var africanLion = new TypeDefinition("AfricanLion", (Trait.Public | Trait.Class))
				.Inherit(predator)
				.Inherit(skill)
				.Add(new MemberDefinition("Region", new TypeDefinition("string"), Trait.Public));

			yield return new object[]
			{
				"generic-type",
				TypeDefinition.ResolveDependencies(new TypeDefinition[]{ africanLion, skill, predator }).ToArray()
			};

			// -----

			var lineItem = new TypeDefinition("LineItem", (Trait.Public | Trait.Class))
				.Add(new MemberDefinition("Position", new TypeDefinition("int"), Trait.Public))
				.Add(new MemberDefinition("Name", new TypeDefinition("string"), Trait.Public));

			var form = new TypeDefinition("Form", (Trait.Public | Trait.Class))
				.Add(new MemberDefinition("Name", new TypeDefinition("string"), Trait.Public))
				.Add(new MemberDefinition("Items", lineItem, Trait.Public | Trait.Array));

			yield return new object[]
			{
				"form",
				TypeDefinition.ResolveDependencies(new TypeDefinition[]{ lineItem, form }).ToArray()
			};
		}

		private static IEnumerable<object[]> GetSpecifications()
		{
			string rootFolder = Path.Combine(Sample.DirectoryName, "specs");
			foreach (string folder in Directory.GetDirectories(rootFolder, "*"))
			{
				string[] sourceFiles = Directory.GetFiles(folder, "*.cs");

				yield return new object[]
				{
					sourceFiles,
					Path.GetFileName(folder)
				};
			}
		}

		private static string UTF8(byte[] x) => Encoding.UTF8.GetString(x);

		#endregion Backing Members
	}
}
