using StargateAPI.Business.Data;
using StargateAPI.Business.Queries;
using StargateAPI.Tests.Helpers;

namespace StargateAPI.Tests.Queries
{
    public class GetPeopleTests : IDisposable
    {
        private readonly StargateContext _context;
        private readonly GetPeopleHandler _handler;

        public GetPeopleTests()
        {
            _context = TestDbContextFactory.Create();
            _handler = new GetPeopleHandler(_context);
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public async Task Handle_NoPeople_ReturnsEmptyList()
        {
            var result = await _handler.Handle(new GetPeople(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Empty(result.People);
        }

        [Fact]
        public async Task Handle_PeopleExist_ReturnsAll()
        {
            _context.People.Add(new Person { Name = "John Doe" });
            _context.People.Add(new Person { Name = "Jane Doe" });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetPeople(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, result.People.Count);
        }
    }
}
