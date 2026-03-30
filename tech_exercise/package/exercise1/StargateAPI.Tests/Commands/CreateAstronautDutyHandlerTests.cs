using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Tests.Helpers;

namespace StargateAPI.Tests.Commands
{
    public class CreateAstronautDutyHandlerTests : IDisposable
    {
        private readonly StargateContext _context;
        private readonly CreateAstronautDutyHandler _handler;

        public CreateAstronautDutyHandlerTests()
        {
            _context = TestDbContextFactory.Create();
            _handler = new CreateAstronautDutyHandler(_context);
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
        public async Task Handle_FirstDuty_CreatesDetailAndDuty()
        {
            var person = await SeedPerson();
            var request = new CreateAstronautDuty
            {
                Name = "John Doe",
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = new DateTime(2024, 1, 15)
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Id);

            var detail = _context.AstronautDetails.FirstOrDefault(d => d.PersonId == person.Id);
            Assert.NotNull(detail);
            Assert.Equal("Commander", detail.CurrentDutyTitle);
            Assert.Equal("1LT", detail.CurrentRank);
            Assert.Equal(new DateTime(2024, 1, 15), detail.CareerStartDate);
            Assert.Null(detail.CareerEndDate);

            var duty = _context.AstronautDuties.FirstOrDefault(d => d.PersonId == person.Id);
            Assert.NotNull(duty);
            Assert.Equal("Commander", duty.DutyTitle);
            Assert.Null(duty.DutyEndDate);
        }

        [Fact]
        public async Task Handle_SubsequentDuty_UpdatesDetailAndClosesPreviousDuty()
        {
            var person = await SeedPerson();

            // Create first duty
            var first = new CreateAstronautDuty
            {
                Name = "John Doe", Rank = "1LT", DutyTitle = "Commander",
                DutyStartDate = new DateTime(2024, 1, 15)
            };
            await _handler.Handle(first, CancellationToken.None);

            // Clear tracker to simulate fresh request scope (as in real API)
            _context.ChangeTracker.Clear();

            // Create second duty
            var second = new CreateAstronautDuty
            {
                Name = "John Doe", Rank = "CPT", DutyTitle = "Pilot",
                DutyStartDate = new DateTime(2024, 6, 1)
            };
            var result = await _handler.Handle(second, CancellationToken.None);

            Assert.True(result.Success);

            // Previous duty should have end date = new start - 1
            var duties = _context.AstronautDuties
                .Where(d => d.PersonId == person.Id)
                .OrderBy(d => d.DutyStartDate).ToList();
            Assert.Equal(2, duties.Count);
            Assert.Equal(new DateTime(2024, 5, 31), duties[0].DutyEndDate);
            Assert.Null(duties[1].DutyEndDate);

            // Detail should reflect new duty
            var detail = _context.AstronautDetails.First(d => d.PersonId == person.Id);
            Assert.Equal("Pilot", detail.CurrentDutyTitle);
            Assert.Equal("CPT", detail.CurrentRank);
        }

        [Fact]
        public async Task Handle_RetiredDuty_SetsCareerEndDate()
        {
            var person = await SeedPerson();

            // Create initial duty
            await _handler.Handle(new CreateAstronautDuty
            {
                Name = "John Doe", Rank = "1LT", DutyTitle = "Commander",
                DutyStartDate = new DateTime(2024, 1, 15)
            }, CancellationToken.None);

            _context.ChangeTracker.Clear();

            // Retire
            await _handler.Handle(new CreateAstronautDuty
            {
                Name = "John Doe", Rank = "1LT", DutyTitle = "RETIRED",
                DutyStartDate = new DateTime(2025, 1, 1)
            }, CancellationToken.None);

            var detail = _context.AstronautDetails.First(d => d.PersonId == person.Id);
            Assert.Equal("RETIRED", detail.CurrentDutyTitle);
            // Rule 7: Career End Date = Retired Start Date - 1 day
            Assert.Equal(new DateTime(2024, 12, 31), detail.CareerEndDate);
        }

        [Fact]
        public async Task Handle_FirstDutyRetired_SetsCareerEndDateCorrectly()
        {
            // Edge case: first ever duty is RETIRED
            await SeedPerson();

            var result = await _handler.Handle(new CreateAstronautDuty
            {
                Name = "John Doe", Rank = "1LT", DutyTitle = "RETIRED",
                DutyStartDate = new DateTime(2025, 1, 1)
            }, CancellationToken.None);

            Assert.True(result.Success);

            var detail = _context.AstronautDetails.First();
            // Rule 7: Career End Date = Retired Start Date - 1 day
            Assert.Equal(new DateTime(2024, 12, 31), detail.CareerEndDate);
        }

        [Fact]
        public async Task Handle_NewDuty_CurrentDutyHasNullEndDate()
        {
            // Rule 4: Current duty has no end date
            await SeedPerson();

            await _handler.Handle(new CreateAstronautDuty
            {
                Name = "John Doe", Rank = "1LT", DutyTitle = "Commander",
                DutyStartDate = new DateTime(2024, 1, 15)
            }, CancellationToken.None);

            var duty = _context.AstronautDuties.First();
            Assert.Null(duty.DutyEndDate);
        }
    }
}
