using Lab11.DTOs;
using Lab11.Exceptions;
using Microsoft.Data.SqlClient;

namespace Lab11.Services;

public class PrescriptionService(IConfiguration configuration) : IPrescriptionService
{
    public async Task AddPrescription(PrescriptionRequestDto prescriptionRequestDto)
    {
        await using SqlConnection conn = new SqlConnection(configuration.GetConnectionString("Default"));
        await using SqlCommand cmd = new SqlCommand("", conn);
        await conn.OpenAsync();
        var transaction = await conn.BeginTransactionAsync();
        cmd.Transaction = transaction as SqlTransaction;

        try
        {
            //Date check
            if (prescriptionRequestDto.DueDate < prescriptionRequestDto.Date)
            {
                throw new ConflictException("Prescription due date is less than the date");
            }

            //Medicaments count
            if (prescriptionRequestDto.Medicaments.Count > 10)
            {
                throw new ConflictException("Too many medicaments");
            }

            //Client exists
            int idPatient;
            cmd.CommandText = @"
            SELECT IdPatient FROM Patient
            WHERE FirstName = @FirstName AND LastName = @LastName AND Birthdate = @Birthdate";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@FirstName", prescriptionRequestDto.Patient.FirstName);
            cmd.Parameters.AddWithValue("@LastName", prescriptionRequestDto.Patient.LastName);
            cmd.Parameters.AddWithValue("@Birthdate", prescriptionRequestDto.Patient.Birthdate);
            var result = await cmd.ExecuteScalarAsync();

            if (result == null)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                INSERT INTO Patient (FirstName, LastName, Birthdate)
                OUTPUT INSERTED.IdPatient
                VALUES (@FirstName, @LastName, @Birthdate)";

                cmd.Parameters.AddWithValue("@FirstName", prescriptionRequestDto.Patient.FirstName);
                cmd.Parameters.AddWithValue("@LastName", prescriptionRequestDto.Patient.LastName);
                cmd.Parameters.AddWithValue("@Birthdate", prescriptionRequestDto.Patient.Birthdate);

                idPatient = (int)await cmd.ExecuteScalarAsync();
            }
            else
            {
                idPatient = (int)result;
            }

            //Doctor exist
            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT COUNT(*) FROM Doctor WHERE IdDoctor = @IdDoctor;";
            cmd.Parameters.AddWithValue("@IdDoctor", prescriptionRequestDto.IdDoctor);
            int count = (int)await cmd.ExecuteScalarAsync();
            if (count <= 0)
            {
                throw new NotFoundException("Prescription doctor not found");
            }

            //Medicaments exists
            foreach (var medicament in prescriptionRequestDto.Medicaments)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = "SELECT COUNT(*) FROM Medicament WHERE IdMedicament = @IdMedicament;";
                cmd.Parameters.AddWithValue("@IdMedicament", medicament.IdMedicament);
                count = (int)await cmd.ExecuteScalarAsync();
                if (count <= 0)
                {
                    throw new NotFoundException($"Medicament {medicament.IdMedicament} not found");
                }
            }

            //Prescription add
            cmd.Parameters.Clear();
            cmd.CommandText = @"INSERT INTO Prescription (Date, DueDate, IdPatient, IdDoctor)
                            OUTPUT INSERTED.IdPrescription
                            VALUES (@Date, @DueDate, @IdPatient, @IdDoctor);";
            cmd.Parameters.AddWithValue("@Date", prescriptionRequestDto.Date);
            cmd.Parameters.AddWithValue("@DueDate", prescriptionRequestDto.DueDate);
            cmd.Parameters.AddWithValue("@IdPatient", idPatient);
            cmd.Parameters.AddWithValue("@IdDoctor", prescriptionRequestDto.IdDoctor);
            int newPrescriptionId = (int)(await cmd.ExecuteScalarAsync());

            //Prescription_medicaments add
            foreach (var medicament in prescriptionRequestDto.Medicaments)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = @"INSERT INTO Prescription_Medicament (IdMedicament, IdPrescription,Dose,Details)
                                     VALUES (@IdMedicament, @IdPrescription, @Dose, @Details);";
                cmd.Parameters.AddWithValue("@IdMedicament", medicament.IdMedicament);
                cmd.Parameters.AddWithValue("@IdPrescription", newPrescriptionId);
                cmd.Parameters.AddWithValue("@Dose", medicament.Dose);
                cmd.Parameters.AddWithValue("@Details", medicament.Details);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PatientGetDto> GetPatient(int id)
    {
        var patientDto = new PatientGetDto();
        var prescriptionsDict = new Dictionary<int, PrescriptionDto>();

        string query = @"
        SELECT P.FirstName AS PName,
            P.LastName,
            P.Birthdate,
            PS.IdPrescription,
            PS.Date,
            PS.DueDate,
            M.IdMedicament,
            M.Name,
            PM.Dose,
            M.Description,
            D.IdDoctor,
            D.FirstName AS DName
        FROM Patient P
         INNER JOIN Prescription PS on P.IdPatient = PS.IdPatient
         INNER JOIN Prescription_Medicament PM on PS.IdPrescription = PM.IdPrescription
         INNER JOIN Medicament M on PM.IdMedicament = M.IdMedicament
         INNER JOIN Doctor D on D.IdDoctor = PS.IdDoctor
        WHERE P.IdPatient = @id;
        ";

        using (SqlConnection conn = new SqlConnection(configuration.GetConnectionString("Default")))
        using (SqlCommand cmd = new SqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            await conn.OpenAsync();
            var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (patientDto.IdPatient == 0)
                {
                    patientDto.IdPatient = id;
                    patientDto.FirstName = reader.GetString(reader.GetOrdinal("PName"));
                    patientDto.LastName = reader.GetString(reader.GetOrdinal("LastName"));
                    patientDto.Birthdate = reader.GetDateTime(reader.GetOrdinal("Birthdate"));
                }

                int prescriptionId = reader.GetInt32(reader.GetOrdinal("IdPrescription"));
                if (!prescriptionsDict.ContainsKey(prescriptionId))
                {
                    prescriptionsDict[prescriptionId] = new PrescriptionDto()
                    {
                        IdPrescription = prescriptionId,
                        Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                        DueDate = reader.GetDateTime(reader.GetOrdinal("DueDate")),
                        Doctor = new DoctorDto
                        {
                            IdDoctor = reader.GetInt32(reader.GetOrdinal("IdDoctor")),
                            FirstName = reader.GetString(reader.GetOrdinal("DName")),
                        },
                        Medicaments = new List<MedicamentGetDto>()
                    };
                }

                prescriptionsDict[prescriptionId].Medicaments.Add(new MedicamentGetDto()
                {
                    IdMedicament = reader.GetInt32(reader.GetOrdinal("IdMedicament")),
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    Dose = reader.GetInt32(reader.GetOrdinal("Dose")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                });

                patientDto.Prescriptions = prescriptionsDict.Values
                    .OrderBy(p => p.DueDate)
                    .ToList();
            }
        }

        if (patientDto is null)
        {
            throw new NotFoundException("Patient not found");
        }

        return patientDto;
    }
}