using Microsoft.AspNetCore.Http;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Tests.Helpers;

namespace StargateAPI.Tests.Commands
{
    public class CreatePersonTests : IDisposable
    {
        private readonly StargateContext _context;

        public CreatePersonTests()
        {
            _context = TestDbContextFactory.Create();
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public async Task PreProcessor_DuplicateName_ThrowsBadRequest()
        {
            _context.People.Add(new Person { Name = "John Doe" });
            await _context.SaveChangesAsync();

            var processor = new CreatePersonPreProcessor(_context);
            var request = new CreatePerson { Name = "John Doe" };

            var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => processor.Process(request, CancellationToken.None));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public async Task PreProcessor_EmptyName_ThrowsBadRequest()
        {
            var processor = new CreatePersonPreProcessor(_context);
            var request = new CreatePerson { Name = "  " };

            var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => processor.Process(request, CancellationToken.None));
            Assert.Contains("required", ex.Message);
        }

        [Fact]
        public async Task PreProcessor_ValidName_DoesNotThrow()
        {
            var processor = new CreatePersonPreProcessor(_context);
            var request = new CreatePerson { Name = "John Doe" };

            await processor.Process(request, CancellationToken.None);
        }

        [Fact]
        public async Task Handler_ValidName_CreatesPersonAndReturnsId()
        {
            var handler = new CreatePersonHandler(_context);
            var request = new CreatePerson { Name = "John Doe" };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Id > 0);

            var person = _context.People.FirstOrDefault(p => p.Name == "John Doe");
            Assert.NotNull(person);
        }
    }
}
