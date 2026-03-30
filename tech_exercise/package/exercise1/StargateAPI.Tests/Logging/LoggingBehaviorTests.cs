using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Logging;
using StargateAPI.Tests.Helpers;

namespace StargateAPI.Tests.Logging
{
    public class LoggingBehaviorTests : IDisposable
    {
        private readonly StargateAPI.Business.Data.StargateContext _context;

        public LoggingBehaviorTests()
        {
            _context = TestDbContextFactory.Create();
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public async Task Handle_SuccessfulRequest_LogsToDatabase()
        {
            var logger = new Mock<ILogger<LoggingBehavior<CreatePerson, CreatePersonResult>>>();
            var behavior = new LoggingBehavior<CreatePerson, CreatePersonResult>(_context, logger.Object);

            var request = new CreatePerson { Name = "Test" };
            var expectedResult = new CreatePersonResult { Id = 1 };

            var result = await behavior.Handle(
                request,
                () => Task.FromResult(expectedResult),
                CancellationToken.None);

            Assert.Equal(expectedResult, result);

            var log = _context.ProcessLogs.FirstOrDefault();
            Assert.NotNull(log);
            Assert.Equal("CreatePerson", log.RequestType);
            Assert.True(log.Success);
            Assert.Null(log.ErrorMessage);
        }

        [Fact]
        public async Task Handle_FailedRequest_LogsExceptionToDatabase()
        {
            var logger = new Mock<ILogger<LoggingBehavior<CreatePerson, CreatePersonResult>>>();
            var behavior = new LoggingBehavior<CreatePerson, CreatePersonResult>(_context, logger.Object);

            var request = new CreatePerson { Name = "Test" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                behavior.Handle(
                    request,
                    () => throw new InvalidOperationException("Test error"),
                    CancellationToken.None));

            var log = _context.ProcessLogs.FirstOrDefault();
            Assert.NotNull(log);
            Assert.Equal("CreatePerson", log.RequestType);
            Assert.False(log.Success);
            Assert.Equal("Test error", log.ErrorMessage);
            Assert.Equal(500, log.ResponseCode);
        }
    }
}
