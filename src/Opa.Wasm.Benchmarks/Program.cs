﻿using System;
using Wasmtime;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Opa.Wasm;

namespace Opa.Wasm.Benchmarks
{
	public class WasmPolicyExecution
	{
		private readonly OpaModule _opaModule;
		private readonly Module _module;

		public WasmPolicyExecution()
		{
			var policyBytes = System.IO.File.ReadAllBytes("example.wasm");

			_opaModule = new OpaModule();
			_module = _opaModule.Load("example", policyBytes);
		}

		[Benchmark]
		public string RunPolicy()
		{
			using var opaPolicy = new OpaPolicy(_opaModule, _module);

			opaPolicy.SetData(@"{""world"": ""world""}");

			string input = @"{""message"": ""world""}";
			string output = opaPolicy.Evaluate(input);

			return output;
		}

		[Benchmark]
		public string FastEvaluatePolicy()
		{
			using var opaPolicy = new OpaPolicy(_opaModule, _module);

			opaPolicy.SetData(@"{""world"": ""world""}");

			string input = @"{""message"": ""world""}";
			string output = opaPolicy.FastEvaluate(input);

			return output;
		}

		const int runXTimes = 100;

		[Benchmark]
		public void RunPolicyX()
		{
			using var opaPolicy = new OpaPolicy(_opaModule, _module);

			opaPolicy.SetData(@"{""world"": ""world""}");

			string input = @"{""message"": ""world""}";
			for (int i = 1; i <= runXTimes; i++)
			{
				string output = opaPolicy.Evaluate(input);
			}
		}

		[Benchmark]
		public void FastEvaluatePolicyX()
		{
			using var opaPolicy = new OpaPolicy(_opaModule, _module);

			opaPolicy.SetData(@"{""world"": ""world""}");

			string input = @"{""message"": ""world""}";

			for (int i = 1; i <= runXTimes; i++)
			{
				string output = opaPolicy.FastEvaluate(input);
			}
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
		}
	}
}
