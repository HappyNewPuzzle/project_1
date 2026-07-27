public sealed record ConnectionAdmissionResult(
    ConnectionAdmissionStatus Status,
    ConnectionAdmissionLease? Lease)
{
    public bool Accepted => Status == ConnectionAdmissionStatus.Accepted;
}
