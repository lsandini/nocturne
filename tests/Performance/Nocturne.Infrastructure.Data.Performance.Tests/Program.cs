using BenchmarkDotNet.Running;
using Nocturne.Infrastructure.Data.Performance.Tests.Infrastructure;

BenchmarkSwitcher.FromAssembly(typeof(PostgresFixture).Assembly).Run(args);
