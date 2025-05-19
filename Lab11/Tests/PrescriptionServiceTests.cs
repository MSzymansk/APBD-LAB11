using Xunit;
using Lab11.Services;
using Lab11.DTOs;
using Lab11.Data;
using Lab11.Models;
using Microsoft.EntityFrameworkCore;


public class PrescriptionServiceTests
{
    private PrescriptionService GetServiceWithInMemoryDb(out DatabaseContext context)
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) 
            .Options;

        context = new DatabaseContext(options);

        context.Doctors.Add(new Doctor
        {
            IdDoctor = 1,
            FirstName = "Anna",
            LastName = "Nowak",
            Email = "anna.nowak@example.com"
        });

        context.Medicaments.Add(new Medicament
        {
            IdMedicament = 1,
            Name = "Apap",
            Description = "Przeciwbólowy",
            Type = "Tabletka"
        });

        context.SaveChanges();

        return new PrescriptionService(context);
    }

    [Fact]
    public async Task PostPrescription()
    {
        var service = GetServiceWithInMemoryDb(out var context);

        var prescription = new PrescriptionRequestDto
        {
            Patient = new PatientDto
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Birthdate = new DateTime(1990, 5, 19)
            },
            Medicaments = new List<MedicamentDto>
            {
                new MedicamentDto
                {
                    IdMedicament = 1,
                    Dose = 2,
                    Details = "Stosować dwa razy dziennie"
                }
            },
            Date = new DateTime(2025, 5, 19),
            DueDate = new DateTime(2025, 5, 26),
            IdDoctor = 1
        };

        await service.AddPrescription(prescription);

        var addedPrescription = await context.Prescriptions.FirstOrDefaultAsync();
        Assert.NotNull(addedPrescription);
        Assert.Equal(1, addedPrescription.IdDoctor);
    }

    [Fact]
    public async Task GetPatient()
    {
        var service = GetServiceWithInMemoryDb(out var context);

        var patient = new Patient
        {
            FirstName = "Jan",
            LastName = "Kowalski",
            Birthdate = new DateTime(1990, 5, 19)
        };

        context.Patients.Add(patient);
        context.SaveChanges();

        var prescription = new Prescription
        {
            IdPatient = patient.IdPatient,
            IdDoctor = 1,
            Date = new DateTime(2025, 5, 19),
            DueDate = new DateTime(2025, 5, 26),
            PrescriptionMedicaments = new List<PrescriptionMedicament>
            {
                new PrescriptionMedicament
                {
                    IdMedicament = 1,
                    Dose = 1,
                    Details = "Stosować raz dziennie"
                }
            }
        };

        context.Prescriptions.Add(prescription);
        context.SaveChanges();

        var result = await service.GetPatient(patient.IdPatient);

        Assert.NotNull(result);
        Assert.Equal(patient.FirstName, result.FirstName);
        Assert.Single(result.Prescriptions);
        Assert.Equal(1, result.Prescriptions[0].Medicaments[0].IdMedicament);
    }
}
