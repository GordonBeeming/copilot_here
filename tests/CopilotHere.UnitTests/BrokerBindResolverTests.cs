using System.Net;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class BrokerBindResolverTests
{
  [Test]
  public async Task ResolveTcpBindAddress_NoEnv_ReturnsAny()
  {
    var env = new Dictionary<string, string?>();
    var result = BrokerBindResolver.ResolveTcpBindAddress(env);
    await Assert.That(result).IsEqualTo(IPAddress.Any);
  }

  [Test]
  public async Task ResolveTcpBindAddress_EmptyEnv_ReturnsAny()
  {
    var env = new Dictionary<string, string?> { [BrokerBindResolver.BindLoopbackEnvVar] = "" };
    var result = BrokerBindResolver.ResolveTcpBindAddress(env);
    await Assert.That(result).IsEqualTo(IPAddress.Any);
  }

  [Test]
  [Arguments("1")]
  [Arguments("true")]
  [Arguments("TRUE")]
  [Arguments("True")]
  [Arguments("yes")]
  [Arguments("on")]
  [Arguments(" 1 ")]
  public async Task ResolveTcpBindAddress_TruthyEnv_ReturnsLoopback(string value)
  {
    var env = new Dictionary<string, string?> { [BrokerBindResolver.BindLoopbackEnvVar] = value };
    var result = BrokerBindResolver.ResolveTcpBindAddress(env);
    await Assert.That(result).IsEqualTo(IPAddress.Loopback);
  }

  [Test]
  [Arguments("0")]
  [Arguments("false")]
  [Arguments("no")]
  [Arguments("off")]
  [Arguments("anything-else")]
  public async Task ResolveTcpBindAddress_FalsyOrUnknownEnv_ReturnsAny(string value)
  {
    var env = new Dictionary<string, string?> { [BrokerBindResolver.BindLoopbackEnvVar] = value };
    var result = BrokerBindResolver.ResolveTcpBindAddress(env);
    await Assert.That(result).IsEqualTo(IPAddress.Any);
  }
}
