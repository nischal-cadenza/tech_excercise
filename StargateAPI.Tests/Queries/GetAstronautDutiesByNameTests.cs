using StargateAPI.Business.Data;
using StargateAPI.Business.Queries;
using StargateAPI.Tests.Helpers;
using System.Net;

namespace StargateAPI.Tests.Queries
{
    public class GetAstronautDutiesByNameTests : IDisposable
    {
        private readonly StargateContext _context;
        private readonly GetAstronautDutiesByNameHandler _handler;

        public GetAstronautDutiesByNameTests()
        {
            _context = TestDbContextFactory.Create();
            _handler = new GetAstronautDutiesByNameHandler(_context);
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public async Task Handle_PersonNotFound_Returns404()
        {
            // BUG 3 regression test: should not throw NullReferenceException
            var request = new GetAstronautDutiesByName { Name = "Nobody" };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal((int)HttpStatusCode.NotFound, result.ResponseCode);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public async Task Handle_EmptyName_ReturnsBadRequest()
        {
            var request = new GetAstronautDutiesByName { Name = "" };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal((int)HttpStatusCode.BadRequest, result.ResponseCode);
        }

        [Fact]
        public async Task Handle_PersonWithDuties_ReturnsDuties()
        {
            var person = new Person { Name = "John Doe" };
            _context.People.Add(person);
            await _context.SaveChangesAsync();

            _context.AstronautDetails.Add(new AstronautDetail
            {
                PersonId = person.Id,
                CurrentRank = "CPT",
                CurrentDutyTitle = "Pilot",
                CareerStartDate = new DateTime(2024, 1, 15)
            });
            _context.AstronautDuties.Add(new AstronautDuty
            {
                PersonId = person.Id,
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = new DateTime(2024, 1, 15),
                DutyEndDate = new DateTime(2024, 5, 31)
            });
            _context.AstronautDuties.Add(new AstronautDuty
            {
                PersonId = person.Id,
                Rank = "CPT",
                DutyTitle = "Pilot",
                DutyStartDate = new DateTime(2024, 6, 1)
            });
            await _context.SaveChangesAsync();

            var request = new GetAstronautDutiesByName { Name = "John Doe" };
            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Person);
            Assert.Equal("John Doe", result.Person.Name);
            Assert.Equal(2, result.AstronautDuties.Count);
        }

        [Fact]
        public async Task Handle_PersonWithoutDuties_ReturnsEmptyList()
        {
            _context.People.Add(new Person { Name = "John Doe" });
            await _context.SaveChangesAsync();

            var request = new GetAstronautDutiesByName { Name = "John Doe" };
            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Person);
            Assert.Empty(result.AstronautDuties);
        }
    }
}
