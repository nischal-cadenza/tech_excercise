using Microsoft.AspNetCore.Http;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Tests.Helpers;

namespace StargateAPI.Tests.Commands
{
    public class CreateAstronautDutyPreProcessorTests : IDisposable
    {
        private readonly StargateContext _context;
        private readonly CreateAstronautDutyPreProcessor _processor;

        public CreateAstronautDutyPreProcessorTests()
        {
            _context = TestDbContextFactory.Create();
            _processor = new CreateAstronautDutyPreProcessor(_context);
        }

        public void Dispose() => _context.Dispose();

        private async Task<Person> SeedPerson(string name = "John Doe")
        {
            var person = new Person { Name = name };
            await _context.People.AddAsync(person);
            await _context.SaveChangesAsync();
            return person;
        }

        [Fact]
        public async Task Process_PersonNotFound_ThrowsBadRequest()
        {
            var request = new CreateAstronautDuty
            {
                Name = "Nobody",
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = DateTime.Now
            };

            var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => _processor.Process(request, CancellationToken.None));
            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public async Task Process_EmptyName_ThrowsBadRequest()
        {
            var request = new CreateAstronautDuty
            {
                Name = "",
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = DateTime.Now
            };

            var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => _processor.Process(request, CancellationToken.None));
            Assert.Contains("Name is required", ex.Message);
        }

        [Fact]
        public async Task Process_EmptyRank_ThrowsBadRequest()
        {
            await SeedPerson();
            var request = new CreateAstronautDuty
            {
                Name = "John Doe",
                Rank = "",
                DutyTitle = "Commander",
                DutyStartDate = DateTime.Now
            };

            var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => _processor.Process(request, CancellationToken.None));
            Assert.Contains("Rank is required", ex.Message);
        }

        [Fact]
        public async Task Process_EmptyDutyTitle_ThrowsBadRequest()
        {
            await SeedPerson();
            var request = new CreateAstronautDuty
            {
                Name = "John Doe",
                Rank = "1LT",
                DutyTitle = "",
                DutyStartDate = DateTime.Now
            };

            var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => _processor.Process(request, CancellationToken.None));
            Assert.Contains("DutyTitle is required", ex.Message);
        }

        [Fact]
        public async Task Process_DuplicateDutyForSamePerson_ThrowsBadRequest()
        {
            var person = await SeedPerson();
            var startDate = new DateTime(2024, 1, 15);

            _context.AstronautDuties.Add(new AstronautDuty
            {
                PersonId = person.Id,
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = startDate
            });
            await _context.SaveChangesAsync();

            var request = new CreateAstronautDuty
            {
                Name = "John Doe",
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = startDate
            };

            var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => _processor.Process(request, CancellationToken.None));
            Assert.Contains("already has duty", ex.Message);
        }

        [Fact]
        public async Task Process_DuplicateDutyForDifferentPerson_DoesNotThrow()
        {
            // BUG 4 regression test: different people CAN have same duty on same date
            var person1 = await SeedPerson("John Doe");
            var person2 = await SeedPerson("Jane Doe");
            var startDate = new DateTime(2024, 1, 15);

            _context.AstronautDuties.Add(new AstronautDuty
            {
                PersonId = person1.Id,
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = startDate
            });
            await _context.SaveChangesAsync();

            var request = new CreateAstronautDuty
            {
                Name = "Jane Doe",
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = startDate
            };

            // Should NOT throw
            await _processor.Process(request, CancellationToken.None);
        }

        [Fact]
        public async Task Process_RetiredPerson_ThrowsBadRequest()
        {
            var person = await SeedPerson();

            _context.AstronautDetails.Add(new AstronautDetail
            {
                PersonId = person.Id,
                CurrentRank = "1LT",
                CurrentDutyTitle = "RETIRED",
                CareerStartDate = new DateTime(2020, 1, 1),
                CareerEndDate = new DateTime(2024, 12, 31)
            });
            await _context.SaveChangesAsync();

            var request = new CreateAstronautDuty
            {
                Name = "John Doe",
                Rank = "CPT",
                DutyTitle = "Pilot",
                DutyStartDate = new DateTime(2025, 6, 1)
            };

            var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => _processor.Process(request, CancellationToken.None));
            Assert.Contains("retired", ex.Message);
        }

        [Fact]
        public async Task Process_ValidRequest_DoesNotThrow()
        {
            await SeedPerson();

            var request = new CreateAstronautDuty
            {
                Name = "John Doe",
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = new DateTime(2024, 1, 15)
            };

            await _processor.Process(request, CancellationToken.None);
        }
    }
}
