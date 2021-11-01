using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Opa.Wasm.UnitTests
{

	public class WasmTests
	{
		private static readonly Dictionary<string, string> _exceptions = new Dictionary<string, string>();

		[Test]
		[TestCaseSource(nameof(GetTestCases))]
		public void RunWasmTests(JsonElement testCase)
		{
			// skip any known exceptions
			var note = testCase.GetProperty("note").GetString();
			if (_exceptions.TryGetValue(note, out string exception))
			{
				Assert.Ignore($"{note}:  {exception}");
			}

			// if there is an input term with invalid JSON, then skip it
			if (testCase.TryGetProperty("input_term", out JsonElement inputTerm))
			{
				var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(inputTerm.GetString()));
				if (!JsonDocument.TryParseValue(ref reader, out JsonDocument _))
				{
					Assert.Ignore($"{note}: input_term value format not supported");
				}
			}

			string expected = null;
			if (testCase.TryGetProperty("want_result", out JsonElement expectedElement))
			{
				var expectedResults = expectedElement.EnumerateArray();
				if (expectedResults.Count() > 1)
					Assert.Ignore($"{note}: more than one expected result not supported: {expectedResults.Count()}");
				else
					expected = JsonSerializer.Serialize(expectedResults.First().GetProperty("x")); //.GetRawText();
			}

			var modules = testCase.GetProperty("modules")
				.EnumerateArray()
				.Select(e => e.GetString())
				.ToList();
			var query = testCase.GetProperty("query").GetString();
			var wasmFilePath = CompileToWasm(modules, query);
			var policy = new OpaPolicy(wasmFilePath);

			if (testCase.TryGetProperty("data", out JsonElement dataElement))
			{
				policy.SetData(dataElement.GetRawText());
			}

			var input = testCase.TryGetProperty("data", out JsonElement inputElement) ? inputElement.GetRawText() : null;
			input ??= testCase.TryGetProperty("input_term", out inputElement) ? inputElement.GetRawText() : null;

			bool expectError =
				(ExistsAndIsTrue(testCase, "want_error") || ExistsAndIsTrue(testCase, "want_error_code"))
				&& !ExistsAndIsTrue(testCase, "strict_error");
			if (expectError)
				Assert.Throws<Exception>(() => policy.Evaluate(input));
			else
				Assert.DoesNotThrow(() =>
				{
					string res = policy.Evaluate(input);
					string actual = JsonSerializer.Serialize(JsonDocument.Parse(res).RootElement.EnumerateArray().First().GetProperty("result"));
					Assert.AreEqual(expected, actual);
				});
		}

		/// <throws cref="InvalidOperationException">
		/// when found element is not a boolean.
		/// </throws>
		private bool ExistsAndIsTrue(JsonElement element, string propertyName)
		{
			return element.TryGetProperty(propertyName, out JsonElement foundElement) && foundElement.GetBoolean();
		}

		/// <returns>
		/// Path to WASM policy, or null if the entrypoint is not supported.
		/// </returns>
		private static string CompileToWasm(List<string> modules, string query)
		{
			if (modules.Count < 1)
			{
				Assert.Ignore($"empty modules cases are not supported (got {modules.Count})");
			}

			string entrypoint = query switch
			{
				"data.generated.p = x" => "generated/p",
				"data.test.p = x" => "test/p",
				"data.decoded_object.p = x" => "decoded_object/p",
				_ => null,
			};

			if (entrypoint == null) Assert.Ignore($"entrypoint {query} not supported");

			var moduleFiles = modules.Select(module =>
			{
				var tempFilePath = Path.GetTempFileName();
				File.WriteAllText(tempFilePath, module);
				return tempFilePath;
			});

			var tarFilePath = Path.GetTempFileName() + ".tar";
			var createWasmTarProcessInfo = new ProcessStartInfo("opa", $"build -t wasm -e {entrypoint} -o {tarFilePath} {string.Join(' ', moduleFiles)}")
			{
				RedirectStandardError = true,
			};
			using (var createWasmTarProcess = Process.Start(createWasmTarProcessInfo))
			{
				createWasmTarProcess.WaitForExit();
				if (createWasmTarProcess.ExitCode != 0)
				{
					var error = createWasmTarProcess.StandardError.ReadToEnd();
					Assert.Ignore(error);
				}
			}

			var tempDir = Path.Combine(Path.GetTempPath() + Path.GetRandomFileName());
			Directory.CreateDirectory(tempDir);
			var untarProcessInfo = new ProcessStartInfo("tar", $"xf {tarFilePath} -C {tempDir} /policy.wasm")
			{
				RedirectStandardOutput = true
			};
			using (var untarProcess = Process.Start(untarProcessInfo))
			{
				untarProcess.WaitForExit();
				if (untarProcess.ExitCode != 0)
				{
					Assert.Ignore(untarProcess.StandardOutput.ReadToEnd());
				}
			}

			var wasmFile = Path.Combine(tempDir, "policy.wasm");
			return wasmFile;
		}

		public static IEnumerable GetTestCases()
		{
			string testDataDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestData", "cases", "testdata");
			foreach (string d in Directory.GetDirectories(testDataDir))
			{
				foreach (string f in Directory.GetFiles(d))
				{
					string body = File.ReadAllText(f);
					// Convert YAML to JSON
					var yamlDeserializer = new DeserializerBuilder()
						.WithNamingConvention(UnderscoredNamingConvention.Instance)
						.IgnoreUnmatchedProperties()
						.Build();
					var yaml = yamlDeserializer.Deserialize(new StringReader(body));
					var js = new SerializerBuilder().JsonCompatible().Build();
					var sw = new StringWriter();
					js.Serialize(sw, yaml);
					var json = sw.ToString();
					var jsonDoc = JsonDocument.Parse(json).RootElement;
					foreach (var testCase in jsonDoc.GetProperty("cases").EnumerateArray())
					{
						var data = new TestCaseData(testCase);
						data.SetName(testCase.GetProperty("note").GetString());
						yield return data;
					}
				}
			}
		}
	}
}
