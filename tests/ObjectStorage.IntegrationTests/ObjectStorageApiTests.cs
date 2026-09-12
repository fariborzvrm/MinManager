using System.Net;
using System.Net.Http.Json;
using ObjectStorage.IntegrationTests.Fixtures;

namespace ObjectStorage.IntegrationTests;

/// <summary>
/// Legacy integration tests migrated to use Testcontainers.
/// See ObjectStorageIntegrationTests for the comprehensive test suite.
/// </summary>
public sealed class ObjectStorageApiTests : IntegrationTestBase
{
    public ObjectStorageApiTests(TestcontainersFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Upload_Download_Delete_RoundTrip_Persists_In_Postgres()
    {
        const string content = "integration test content";
        const string fileName = "integration-test.txt";

        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))), "file", fileName);
        form.Add(new StringContent("documents"), "category");

        var uploadResponse = await Client.PostAsync("/api/v1/objects", form);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        Assert.Equal(fileName, uploadResult.FileName);

        var id = uploadResult.Id;

        var metadataResponse = await Client.GetAsync($"/api/v1/objects/{id}/metadata");
        metadataResponse.EnsureSuccessStatusCode();
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(metadata);
        Assert.Equal(id, metadata.Id);
        Assert.Equal(fileName, metadata.FileName);
        Assert.Equal(content.Length, metadata.Size);

        var downloadResponse = await Client.GetAsync($"/api/v1/objects/{id}/download");
        downloadResponse.EnsureSuccessStatusCode();
        var downloaded = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal(content, downloaded);

        var deleteResponse = await Client.DeleteAsync($"/api/v1/objects/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDeleteResponse = await Client.GetAsync($"/api/v1/objects/{id}/metadata");
        Assert.Equal(HttpStatusCode.NotFound, afterDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_MissingApiKey_Returns401()
    {
        var client = Factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream([1, 2, 3])), "file", "test.txt");
        form.Add(new StringContent("documents"), "category");

        var response = await client.PostAsync("/api/v1/objects", form);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_InvalidApiKey_Returns401()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "invalid-key");
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream([1, 2, 3])), "file", "test.txt");
        form.Add(new StringContent("documents"), "category");

        var response = await client.PostAsync("/api/v1/objects", form);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record UploadResponse(
        string Id,
        string FileName,
        string ContentType,
        long Size,
        DateTimeOffset CreatedAt);
}
