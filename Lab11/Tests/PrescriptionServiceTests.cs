using Xunit;
using Lab11.Services;
using Lab11.DTOs;

public class PrescriptionServiceTests
{
    [Fact]
    public async Task PostPrescription()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            {
                "ConnectionStrings:Default",
                "Data Source=db-mssql;Initial Catalog=2019SBD;Integrated Security=True;Encrypt=False;"
            }
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var service = new PrescriptionService(configuration);

        var prescription = new PrescriptionRequestDto
        {
            Patient = new PatientDto
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Birthdate = DateTime.Parse("1990-05-19")
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
            Date = DateTime.Parse("2025-05-19"),
            DueDate = DateTime.Parse("2025-05-26"),
            IdDoctor = 1
        };

        await service.AddPrescription(prescription);

        Assert.True(true);
    }
    
    [Fact]
    public async Task GetPatient()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            {
                "ConnectionStrings:Default",
                "Data Source=db-mssql;Initial Catalog=2019SBD;Integrated Security=True;Encrypt=False;"
            }
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        var service = new PrescriptionService(configuration);
        var result = await service.GetPatient(1);
        Assert.NotNull(result);
        Assert.Equal(1, result.IdPatient);
    }
}