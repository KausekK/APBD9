using System.Runtime.InteropServices.JavaScript;
using lab9.Data;
using lab9.DTO;
using lab9.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lab9.controllers;
[ApiController]
[Route("api/[controller]")]
public class TripsController : ControllerBase
{
    private readonly MasterContext _masterContext;
    public TripsController(MasterContext masterContext)
    {
        _masterContext = masterContext;
    }
    [HttpGet]
    public async Task<IActionResult> GetTrips(int pageNumber = 1, int pageSize = 10)
    {
        var tripsQuery = _masterContext.Trips
            .Include(t => t.ClientTrips).ThenInclude(ct => ct.IdClientNavigation)
            .Include(t => t.IdCountries)
            .OrderBy(t => t.Name)
            .AsQueryable();

        var totalRecords = await tripsQuery.CountAsync();
        
        var trips = await tripsQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize).Select(t => new TripsDTO()
            {
                Name = t.Name,
                Description = t.Description,
                DateFrom = t.DateFrom,
                DateTo = t.DateTo,
                MaxPeople = t.MaxPeople,
                Countries = t.IdCountries.Select(c=> new CountryDTO()
                {
                    Name = c.Name
                }).ToList(),
                Clients = t.ClientTrips.Select(ct => new ClientsDTO()
                {
                    FirstName = ct.IdClientNavigation.FirstName,
                    LastName = ct.IdClientNavigation.LastName
                }).ToList()
            })
            .ToListAsync();
        return Ok(new
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            Trips = trips
        });
    }

    [HttpDelete("{idClient}")]
    public async Task<IActionResult> DeleteClient(int idClient)
    {
        var client = await _masterContext.Clients.FindAsync(idClient);
        if (client == null)
        {
            return NotFound($"Client with ID {idClient} not found.");
        }
        bool hasTrips = await _masterContext.ClientTrips.AnyAsync(ct => ct.IdClient == idClient);
        if (hasTrips)
        {
            return BadRequest($"Client with ID {idClient} is registered for one or more trips and cannot be deleted.");
        }
        _masterContext.Clients.Remove(client);
        await _masterContext.SaveChangesAsync();

        return Ok($"Client with ID {idClient} has been deleted successfully.");
    }

    [HttpPost("{idTrip}/clients")]
   public async Task<IActionResult> RegisterClientToTrip([FromBody] AddClientToTripDTO clientToTripDto)
   {
       var existingClient = await _masterContext.Clients.FirstOrDefaultAsync(c => c.Pesel == clientToTripDto.Pesel);
       
       if (existingClient != null)
       {
           return Conflict($"Client with PESEL {clientToTripDto.Pesel} already exists.");
       }
   
       var newClient = new Client 
       {
           FirstName = clientToTripDto.FirstName,
           LastName = clientToTripDto.LastName,
           Email = clientToTripDto.Email,
           Telephone = clientToTripDto.Telephone,
           Pesel = clientToTripDto.Pesel
       };
       _masterContext.Clients.Add(newClient);
       await _masterContext.SaveChangesAsync(); 
   
       var trip = await _masterContext.Trips.FirstOrDefaultAsync(t => t.IdTrip == clientToTripDto.IdTrip);
       if (trip == null || trip.DateFrom <= DateTime.Now)
       {
           return BadRequest($"Trip ID {clientToTripDto.IdTrip} does not exist or has already started.");
       }
   
       bool isRegistered = await _masterContext.ClientTrips.AnyAsync(ct => ct.IdClient == newClient.IdClient && ct.IdTrip == trip.IdTrip);
       if (isRegistered)
       {
           return Conflict($"Client with PESEL {clientToTripDto.Pesel} is already registered for trip ID {trip.IdTrip}.");
       }
   
       var clientTrip = new ClientTrip
       {
           IdClient = newClient.IdClient,
           IdTrip = trip.IdTrip,
           RegisteredAt = DateTime.UtcNow,
           PaymentDate = clientToTripDto.PaymentDate
       };
       _masterContext.ClientTrips.Add(clientTrip);
       await _masterContext.SaveChangesAsync();
   
       return Ok("Registration successful.");
   }

   
}