using Lab11.Data;
using Lab11.DTOs;
using Lab11.Exceptions;
using Lab11.Models;
using Microsoft.EntityFrameworkCore;

namespace Lab11.Services;

public class PrescriptionService : IPrescriptionService
{
    private readonly DatabaseContext _context;

    public PrescriptionService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task AddPrescription(PrescriptionRequestDto dto)
    {
        if (dto.DueDate < dto.Date)
            throw new ConflictException("Prescription due date is less than the date");

        if (dto.Medicaments.Count > 10)
            throw new ConflictException("Too many medicaments");

        var patient = await _context.Patients
            .FirstOrDefaultAsync(p =>
                p.FirstName == dto.Patient.FirstName &&
                p.LastName == dto.Patient.LastName &&
                p.Birthdate == dto.Patient.Birthdate);

        if (patient == null)
        {
            patient = new Patient
            {
                FirstName = dto.Patient.FirstName,
                LastName = dto.Patient.LastName,
                Birthdate = dto.Patient.Birthdate
            };
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();
        }

        var doctor = await _context.Doctors.FindAsync(dto.IdDoctor);
        if (doctor == null)
            throw new NotFoundException("Prescription doctor not found");

        var medicaments = await _context.Medicaments
            .Where(m => dto.Medicaments.Select(x => x.IdMedicament).Contains(m.IdMedicament))
            .ToListAsync();

        if (medicaments.Count != dto.Medicaments.Count)
            throw new NotFoundException("One or more medicaments not found");

        var prescription = new Prescription
        {
            Date = dto.Date,
            DueDate = dto.DueDate,
            IdDoctor = dto.IdDoctor,
            IdPatient = patient.IdPatient,
            PrescriptionMedicaments = dto.Medicaments.Select(m => new PrescriptionMedicament
            {
                IdMedicament = m.IdMedicament,
                Dose = m.Dose,
                Details = m.Details
            }).ToList()
        };

        _context.Prescriptions.Add(prescription);
        await _context.SaveChangesAsync();
    }

    public async Task<PatientGetDto> GetPatient(int id)
    {
        var patient = await _context.Patients
            .Where(p => p.IdPatient == id)
            .Include(p => p.Prescriptions)
                .ThenInclude(p => p.Doctor)
            .Include(p => p.Prescriptions)
                .ThenInclude(p => p.PrescriptionMedicaments)
                    .ThenInclude(pm => pm.Medicament)
            .FirstOrDefaultAsync();

        if (patient == null)
            throw new NotFoundException("Patient not found");

        return new PatientGetDto
        {
            IdPatient = patient.IdPatient,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            Birthdate = patient.Birthdate,
            Prescriptions = patient.Prescriptions
                .OrderBy(p => p.DueDate)
                .Select(p => new PrescriptionDto
                {
                    IdPrescription = p.IdPrescription,
                    Date = p.Date,
                    DueDate = p.DueDate,
                    Doctor = new DoctorDto
                    {
                        IdDoctor = p.Doctor.IdDoctor,
                        FirstName = p.Doctor.FirstName
                    },
                    Medicaments = p.PrescriptionMedicaments
                        .Select(pm => new MedicamentGetDto
                        {
                            IdMedicament = pm.IdMedicament,
                            Name = pm.Medicament.Name,
                            Description = pm.Medicament.Description,
                            Dose = pm.Dose
                        }).ToList()
                }).ToList()
        };
    }
}
