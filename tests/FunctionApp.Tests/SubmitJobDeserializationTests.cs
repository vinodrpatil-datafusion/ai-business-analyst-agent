using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Contracts.Invocation;
using Xunit;

namespace FunctionApp.Tests;

public class SubmitJobDeserializationTests
{
    [Fact]
    public async Task Deserialize_InvalidJson_ThrowsJsonException()
    {
        var badJson = "{ invalid json }";
        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await JsonSerializer.DeserializeAsync<SubmitJobRequestV1>(
                new MemoryStream(Encoding.UTF8.GetBytes(badJson)));
        });
    }
}
