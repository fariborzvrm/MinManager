using ObjectStorage.Domain.Exceptions;
using ObjectStorage.Infrastructure.MinIO;
using AccessDeniedException = Minio.Exceptions.AccessDeniedException;
using BucketNotFoundException = Minio.Exceptions.BucketNotFoundException;
using DomainObjectNotFoundException = ObjectStorage.Domain.Exceptions.ObjectNotFoundException;
using MinioException = Minio.Exceptions.MinioException;
using ObjectNotFoundException = Minio.Exceptions.ObjectNotFoundException;

namespace ObjectStorage.UnitTests;

public sealed class MinioExceptionMapperTests
{
    [Fact]
    public void Map_WhenSdkObjectNotFound_ReturnsDomainObjectNotFoundException()
    {
        var sdkException = new ObjectNotFoundException("my-key", "does not exist");

        var result = MinioExceptionMapper.Map(sdkException, "documents/svc/01HX/notes.pdf");

        var mapped = Assert.IsType<DomainObjectNotFoundException>(result);
        Assert.Equal("documents/svc/01HX/notes.pdf", mapped.ObjectKey);
        Assert.Same(sdkException, mapped.InnerException);
    }

    [Fact]
    public void Map_WhenBucketNotFound_ReturnsStorageUnavailableException()
    {
        var result = MinioExceptionMapper.Map(new BucketNotFoundException("objects"), "a/b/c.pdf");

        var mapped = Assert.IsType<StorageUnavailableException>(result);
        Assert.IsType<BucketNotFoundException>(mapped.InnerException);
    }

    [Fact]
    public void Map_WhenAccessDenied_ReturnsStorageUnavailableException()
    {
        var result = MinioExceptionMapper.Map(new AccessDeniedException("denied"), "a/b/c.pdf");

        Assert.IsType<StorageUnavailableException>(result);
    }

    [Fact]
    public void Map_WhenGenericMinioException_ReturnsStorageUnavailableException()
    {
        var result = MinioExceptionMapper.Map(new MinioException("boom"), "a/b/c.pdf");

        Assert.IsType<StorageUnavailableException>(result);
    }

    [Fact]
    public void Map_WhenAlreadyDomainException_ReturnsItUnchanged()
    {
        var domainException = new StorageUnavailableException("already mapped");

        var result = MinioExceptionMapper.Map(domainException, "a/b/c.pdf");

        Assert.Same(domainException, result);
    }

    [Fact]
    public void Map_WhenSdkThrowsUnrelatedException_ReturnsOriginalException()
    {
        var unexpected = new InvalidOperationException("something else");

        var result = MinioExceptionMapper.Map(unexpected, "a/b/c.pdf");

        Assert.Same(unexpected, result);
    }
}