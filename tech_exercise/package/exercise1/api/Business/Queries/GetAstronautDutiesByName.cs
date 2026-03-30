using Dapper;
using MediatR;
using StargateAPI.Business.Data;
using StargateAPI.Business.Dtos;
using StargateAPI.Controllers;
using System.Net;

namespace StargateAPI.Business.Queries
{
    public class GetAstronautDutiesByName : IRequest<GetAstronautDutiesByNameResult>
    {
        public string Name { get; set; } = string.Empty;
    }

    public class GetAstronautDutiesByNameHandler : IRequestHandler<GetAstronautDutiesByName, GetAstronautDutiesByNameResult>
    {
        private readonly StargateContext _context;

        public GetAstronautDutiesByNameHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<GetAstronautDutiesByNameResult> Handle(GetAstronautDutiesByName request, CancellationToken cancellationToken)
        {
            var result = new GetAstronautDutiesByNameResult();

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                result.Success = false;
                result.Message = "Name is required.";
                result.ResponseCode = (int)HttpStatusCode.BadRequest;
                return result;
            }

            // FIX: Original used string interpolation: $"...WHERE '{request.Name}' = a.Name"
            // Changed to parameterized query to prevent SQL injection
            var query = "SELECT a.Id as PersonId, a.Name, b.CurrentRank, b.CurrentDutyTitle, b.CareerStartDate, b.CareerEndDate FROM [Person] a LEFT JOIN [AstronautDetail] b on b.PersonId = a.Id WHERE a.Name = @Name";

            var person = await _context.Connection.QueryFirstOrDefaultAsync<PersonAstronaut>(query, new { request.Name });

            // FIX: Original code had no null check here — it accessed person.PersonId directly,
            // causing a NullReferenceException crash when querying for a non-existent person
            if (person == null)
            {
                result.Success = false;
                result.Message = $"Person '{request.Name}' not found.";
                result.ResponseCode = (int)HttpStatusCode.NotFound;
                return result;
            }

            result.Person = person;

            query = "SELECT * FROM [AstronautDuty] WHERE PersonId = @PersonId ORDER BY DutyStartDate DESC";

            var duties = await _context.Connection.QueryAsync<AstronautDuty>(query, new { person.PersonId });

            result.AstronautDuties = duties.ToList();

            return result;
        }
    }

    public class GetAstronautDutiesByNameResult : BaseResponse
    {
        public PersonAstronaut? Person { get; set; }
        public List<AstronautDuty> AstronautDuties { get; set; } = new List<AstronautDuty>();
    }
}
