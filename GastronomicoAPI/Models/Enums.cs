namespace RotiseriaAPI.Models;

public enum UserRole
{
    Owner = 0,
    Admin = 1,
    Manager = 2,
    Employee = 3
}

public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Suspended = 3,
    Cancelled = 4,
    Expired = 5
}

public enum LegalDocumentType
{
    Terms = 0,
    Privacy = 1,
    Subscription = 2,
    Cancellation = 3,
    Support = 4
}

public enum OnboardingStep
{
    BusinessProfile = 0,
    Categories = 1,
    Products = 2,
    Tables = 3,
    Completed = 4
}
