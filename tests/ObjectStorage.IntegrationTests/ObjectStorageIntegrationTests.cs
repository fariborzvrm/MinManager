using System.Net;
using System.Net.Http.Json;
using System.Text;
using ObjectStorage.IntegrationTests.Fixtures;

namespace ObjectStorage.IntegrationTests;

public sealed class ObjectStorageIntegrationTests : IntegrationTestBase
{
    public ObjectStorageIntegrationTests(TestcontainersFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Upload_Download_Delete_RoundTrip()
    {
        const string content = "integration test content for round-trip";
        const string fileName = "round-trip-test.txt";

        // Upload
        var uploadResult = await UploadFileAsync(fileName, content, "documents");
        Assert.NotNull(uploadResult);
        Assert.Equal(fileName, uploadResult.FileName);
        Assert.Equal(content.Length, uploadResult.Size);

        var id = uploadResult.Id;

        // Metadata exists
        var metadata = await GetMetadataAsync(id);
        Assert.Equal(id, metadata.Id);
        Assert.Equal(fileName, metadata.FileName);
        Assert.Equal(content.Length, metadata.Size);

        // Download returns original content
        var downloaded = await DownloadFileAsync(id);
        Assert.Equal(content, downloaded);

        // Delete
        var deleteResponse = await Client.DeleteAsync($"/api/v1/objects/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Metadata gone
        var afterDelete = await Client.GetAsync($"/api/v1/objects/{id}/metadata");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Upload_MultipleFiles_DifferentCategories()
    {
        var pdf = await UploadFileAsync("doc.pdf", "pdf-content", "documents");
        var img = await UploadFileAsync("photo.png", "png-content", "images");

        Assert.NotEqual(pdf.Id, img.Id);

        // Verify both are accessible
        var pdfMeta = await GetMetadataAsync(pdf.Id);
        Assert.Equal(pdf.Id, pdfMeta.Id);

        var imgMeta = await GetMetadataAsync(img.Id);
        Assert.Equal(img.Id, imgMeta.Id);
    }

    [Fact]
    public async Task Download_MissingObject_Returns404()
    {
        var response = await Client.GetAsync("/api/v1/objects/00000000000000000000000000/metadata");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingObject_Returns404()
    {
        var response = await Client.DeleteAsync("/api/v1/objects/00000000000000000000000000");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream([])), "file", "empty.txt");
        form.Add(new StringContent("documents"), "category");

        var response = await Client.PostAsync("/api/v1/objects", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_UnknownCategory_Returns400()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream([1, 2, 3])), "file", "test.txt");
        form.Add(new StringContent("unknown-category"), "category");

        var response = await Client.PostAsync("/api/v1/objects", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_FileTooLarge_Returns413()
    {
        // MaxFileSizeBytes is 10 MB in test config; send 11 MB
        var largeContent = new byte[11 * 1024 * 1024];
        Random.Shared.NextBytes(largeContent);

        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream(largeContent)), "file", "large.bin");
        form.Add(new StringContent("documents"), "category");

        var response = await Client.PostAsync("/api/v1/objects", form);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedContentType_Returns415()
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(new MemoryStream([1, 2, 3]));
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-executable");
        form.Add(fileContent, "file", "binary.exe");
        form.Add(new StringContent("documents"), "category");

        var response = await Client.PostAsync("/api/v1/objects", form);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
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
        client.DefaultRequestHeaders.Add("X-Api-Key", "totally-invalid-key");
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream([1, 2, 3])), "file", "test.txt");
        form.Add(new StringContent("documents"), "category");

        var response = await client.PostAsync("/api/v1/objects", form);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Download_CrossServiceAccess_Returns403()
    {
        // Upload as test-service
        var uploadResult = await UploadFileAsync("secret.txt", "secret-content", "documents");
        Assert.NotNull(uploadResult);

        // Try to access as a different service
        var otherClient = Factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Api-Key", TestcontainersFixture.OtherServiceApiKey);

        var response = await otherClient.GetAsync($"/api/v1/objects/{uploadResult.Id}/download");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PresignedUrl_Generate_ForExistingObject()
    {
        var uploadResult = await UploadFileAsync("presigned-test.txt", "content", "documents");
        Assert.NotNull(uploadResult);

        var request = new { ExpirationSeconds = 3600 };
        var response = await Client.PostAsJsonAsync($"/api/v1/objects/{uploadResult.Id}/presigned-url", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PresignedUrlResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Url);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task PresignedUrl_UnknownId_Returns404()
    {
        var request = new { ExpirationSeconds = 3600 };
        var response = await Client.PostAsJsonAsync(
            "/api/v1/objects/00000000000000000000000000/presigned-url", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PresignedUrl_ZeroExpiration_Returns400()
    {
        var uploadResult = await UploadFileAsync("presigned-zero.txt", "content", "documents");
        Assert.NotNull(uploadResult);

        var request = new { ExpirationSeconds = 0 };
        var response = await Client.PostAsJsonAsync($"/api/v1/objects/{uploadResult.Id}/presigned-url", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IdempotentRetry_DuplicateKey_ReturnsSameResult()
    {
        var idempotencyKey = Guid.NewGuid().ToString("D");

        // First upload
        var result1 = await UploadFileIdempotentAsync("idem-test.txt", "content", "documents", idempotencyKey);
        Assert.NotNull(result1);

        // Duplicate upload with same key
        var result2 = await UploadFileIdempotentAsync("idem-test.txt", "content", "documents", idempotencyKey);
        Assert.NotNull(result2);

        Assert.Equal(result1.Id, result2.Id);
    }

    [Fact]
    public async Task HealthReady_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Download_BinaryContent()
    {
        var binaryContent = new byte[1024];
        Random.Shared.NextBytes(binaryContent);

        using var uploadForm = new MultipartFormDataContent();
        uploadForm.Add(new StreamContent(new MemoryStream(binaryContent)), "file", "binary.bin");
        uploadForm.Add(new StringContent("documents"), "category");

        var uploadResponse = await Client.PostAsync("/api/v1/objects", uploadForm);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);

        // Download as byte array
        var downloadResponse = await Client.GetAsync($"/api/v1/objects/{uploadResult.Id}/download");
        downloadResponse.EnsureSuccessStatusCode();
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(binaryContent, downloadedBytes);
    }

    // --- Helper methods ---

    private async Task<UploadResponse?> UploadFileAsync(string fileName, string content, string category)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(content))), "file", fileName);
        form.Add(new StringContent(category), "category");

        var response = await Client.PostAsync("/api/v1/objects", form);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UploadResponse>();
    }

    private async Task<UploadResponse?> UploadFileIdempotentAsync(
        string fileName, string content, string category, string idempotencyKey)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(content))), "file", fileName);
        form.Add(new StringContent(category), "category");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/objects")
        {
            Content = form
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UploadResponse>();
    }

    private async Task<UploadResponse> GetMetadataAsync(string id)
    {
        var response = await Client.GetAsync($"/api/v1/objects/{id}/metadata");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UploadResponse>())!;
    }

    private async Task<string> DownloadFileAsync(string id)
    {
        var response = await Client.GetAsync($"/api/v1/objects/{id}/download");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // --- DTOs ---

    public sealed record UploadResponse(
        string Id,
        string FileName,
        string ContentType,
        long Size,
        DateTimeOffset CreatedAt,
        string? Category = null);

    public sealed record PresignedUrlResponse(
        string Url,
        DateTimeOffset ExpiresAt);
}
