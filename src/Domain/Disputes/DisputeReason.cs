namespace Domain.Disputes;

public enum DisputeReason
{
    Unauthorised = 0,
    Duplicate = 1,
    IncorrectAmount = 2,
    GoodsOrServicesNotReceived = 3,
    CancelledSubscription = 4,
    Other = 99,
}
