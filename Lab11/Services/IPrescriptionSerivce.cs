using Lab11.DTOs;

namespace Lab11.Services;

public interface IPrescriptionService
{
    Task AddPrescription(PrescriptionRequestDto prescriptionRequestDto);
    Task<PatientGetDto> GetPatient(int id);
}