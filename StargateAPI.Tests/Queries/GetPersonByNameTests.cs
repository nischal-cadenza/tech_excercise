using StargateAPI.Business.Data;
using StargateAPI.Business.Queries;
using StargateAPI.Tests.Helpers;
using System.Net;

namespace StargateAPI.Tests.Queries
{
    public class GetPersonByNameTests : IDisposable
    {
        private readonly StargateContext _context;
        private readonly GetPersonByNameHandler _handler;

        public GetPersonByNameTests()
        {
            _context = TestDbContextFactory.Create();
            _handler = new GetPersonByNameHandler(_context);
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public async Task Handle_ExistingPerson_ReturnsPerson()
        {
            _context.People.Add(new Person { Name = "John Doe" });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(
                new GetPersonByName { Name = "John Doe" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Person);
            Assert.Equal("John Doe", result.Person.Name);
        }

        [Fact]
        public async Task Handle_NonExistentPerson_Returns404()
        {
            var result = await _handler.Handle(
                new GetPersonByName { Name = "Nobody" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal((int)HttpStatusCode.NotFound, result.ResponseCode);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public async Task Handle_EmptyName_ReturnsBadRequest()
        {
            var result = await _handler.Handle(
                new GetPersonByName { Name = "" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal((int)HttpStatusCode.BadRequest, result.ResponseCode);
        }
    }
}
