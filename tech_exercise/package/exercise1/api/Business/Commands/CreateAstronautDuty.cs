using Dapper;
using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using System.Net;

namespace StargateAPI.Business.Commands
{
    public class CreateAstronautDuty : IRequest<CreateAstronautDutyResult>
    {
        public required string Name { get; set; }

        public required string Rank { get; set; }

        public required string DutyTitle { get; set; }

        public DateTime DutyStartDate { get; set; }
    }

    public class CreateAstronautDutyPreProcessor : IRequestPreProcessor<CreateAstronautDuty>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyPreProcessor(StargateContext context)
        {
            _context = context;
        }

        public Task Process(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            // FIX: Added input validation — original code had no validation at all,
            // allowing empty strings and default dates to reach the database
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new BadHttpRequestException("Name is required.");

            if (string.IsNullOrWhiteSpace(request.Rank))
                throw new BadHttpRequestException("Rank is required.");

            if (string.IsNullOrWhiteSpace(request.DutyTitle))
                throw new BadHttpRequestException("DutyTitle is required.");

            if (request.DutyStartDate == default)
                throw new BadHttpRequestException("DutyStartDate is required.");

            var person = _context.People.AsNoTracking().FirstOrDefault(z => z.Name == request.Name);

            // FIX: Changed from generic "Bad Request" to descriptive message
            if (person is null) throw new BadHttpRequestException($"Person '{request.Name}' not found.");

            // FIX: Added business rule — retired astronauts cannot receive new duties (Rule 6)
            var existingDetail = _context.AstronautDetails
                .AsNoTracking()
                .FirstOrDefault(z => z.PersonId == person.Id);

            if (existingDetail != null && existingDetail.CurrentDutyTitle == "RETIRED")
                throw new BadHttpRequestException($"Person '{request.Name}' is retired and cannot receive new duties.");

            // FIX: Original code checked ALL astronauts globally for same DutyTitle+DutyStartDate.
            // This wrongly prevented two different people from having the same duty on the same date.
            // Now correctly scoped to the specific person via PersonId filter.
            var verifyNoPreviousDuty = _context.AstronautDuties
                .AsNoTracking()
                .FirstOrDefault(z => z.PersonId == person.Id
                    && z.DutyTitle == request.DutyTitle
                    && z.DutyStartDate == request.DutyStartDate);

            if (verifyNoPreviousDuty is not null)
                throw new BadHttpRequestException($"Person '{request.Name}' already has duty '{request.DutyTitle}' starting on {request.DutyStartDate:yyyy-MM-dd}.");

            // FIX: Added chronological order validation — ensures duty timeline integrity
            var currentDuty = _context.AstronautDuties
                .AsNoTracking()
                .Where(z => z.PersonId == person.Id && z.DutyEndDate == null)
                .FirstOrDefault();

            if (currentDuty != null && request.DutyStartDate.Date <= currentDuty.DutyStartDate.Date)
                throw new BadHttpRequestException("New duty start date must be after the current duty start date.");

            return Task.CompletedTask;
        }
    }

    public class CreateAstronautDutyHandler : IRequestHandler<CreateAstronautDuty, CreateAstronautDutyResult>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<CreateAstronautDutyResult> Handle(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            // FIX: All Dapper queries changed from string interpolation to parameterized queries.
            // Original: $"SELECT * FROM [Person] WHERE '{request.Name}' = Name"
            // This was a SQL injection vulnerability — an attacker could inject via the Name field.
            var query = "SELECT * FROM [Person] WHERE Name = @Name";
            var person = await _context.Connection.QueryFirstOrDefaultAsync<Person>(query, new { request.Name });

            query = "SELECT * FROM [AstronautDetail] WHERE PersonId = @PersonId";
            var astronautDetail = await _context.Connection.QueryFirstOrDefaultAsync<AstronautDetail>(query, new { PersonId = person!.Id });

            if (astronautDetail == null)
            {
                astronautDetail = new AstronautDetail();
                astronautDetail.PersonId = person.Id;
                astronautDetail.CurrentDutyTitle = request.DutyTitle;
                astronautDetail.CurrentRank = request.Rank;
                astronautDetail.CareerStartDate = request.DutyStartDate.Date;
                if (request.DutyTitle == "RETIRED")
                {
                    // FIX: Rule 7 — Career End Date = Retired Start Date - 1 day.
                    // Original code set CareerEndDate = DutyStartDate (without subtracting a day).
                    // The else branch below was already correct; this branch was the bug.
                    astronautDetail.CareerEndDate = request.DutyStartDate.AddDays(-1).Date;
                }

                await _context.AstronautDetails.AddAsync(astronautDetail);
            }
            else
            {
                astronautDetail.CurrentDutyTitle = request.DutyTitle;
                astronautDetail.CurrentRank = request.Rank;
                if (request.DutyTitle == "RETIRED")
                {
                    // Rule 7: Career End Date = Retired Start Date - 1 day
                    astronautDetail.CareerEndDate = request.DutyStartDate.AddDays(-1).Date;
                }
                _context.AstronautDetails.Update(astronautDetail);
            }

            // Rule 5: Previous duty end date = new duty start date - 1 day
            query = "SELECT * FROM [AstronautDuty] WHERE PersonId = @PersonId ORDER BY DutyStartDate DESC";
            var astronautDuty = await _context.Connection.QueryFirstOrDefaultAsync<AstronautDuty>(query, new { PersonId = person.Id });

            if (astronautDuty != null)
            {
                astronautDuty.DutyEndDate = request.DutyStartDate.AddDays(-1).Date;
                _context.AstronautDuties.Update(astronautDuty);
            }

            var newAstronautDuty = new AstronautDuty()
            {
                PersonId = person.Id,
                Rank = request.Rank,
                DutyTitle = request.DutyTitle,
                DutyStartDate = request.DutyStartDate.Date,
                DutyEndDate = null
            };

            await _context.AstronautDuties.AddAsync(newAstronautDuty);

            await _context.SaveChangesAsync();

            return new CreateAstronautDutyResult()
            {
                Id = newAstronautDuty.Id
            };
        }
    }

    public class CreateAstronautDutyResult : BaseResponse
    {
        public int? Id { get; set; }
    }
}
