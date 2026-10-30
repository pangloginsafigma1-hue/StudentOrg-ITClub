namespace ITClubOrg.Models;

public sealed class MemberPortalViewModel
{
    public Member Member { get; init; } = new();

    public IReadOnlyList<Activity> UpcomingActivities { get; init; } = [];

    public IReadOnlyList<MemberAttendanceRecord> Attendance { get; init; } = [];

    public IReadOnlyList<MemberAnnouncement> Announcements { get; init; } = [];

    public string? DatabaseError { get; init; }
}

public sealed class MemberAttendanceRecord
{
    public string ActivityName { get; init; } = string.Empty;

    public DateTime ActivityDate { get; init; }

    public DateTime ActivityEndDate { get; init; }

    public bool IsPresent { get; init; }
}

public sealed class MemberStatusHistory
{
    public string Status { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Reason { get; init; }
}

public sealed class ActivityRegistration
{
    public int ActivityId { get; init; }
    public string ActivityName { get; init; } = string.Empty;
    public string MemberId { get; init; } = string.Empty;
    public string MemberName { get; init; } = string.Empty;
    public DateTime RegisteredAt { get; init; }
    public bool Participated { get; init; }
    public string? ParticipationType { get; init; }
    public string? Notes { get; init; }
    public int? RecordedBy { get; init; }
    public DateTime? RecordedAt { get; init; }
}

public sealed class MemberAnnouncement
{
    public int AnnouncementId { get; init; }

    public int? DivisionId { get; init; }

    public int? DivisionId2 { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string PublisherName { get; init; } = string.Empty;

    public string PublisherPosition { get; init; } = string.Empty;

    public string? DivisionName { get; init; }
}

public sealed class MemberRegistrationRequest
{
    public string MemberId { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string Course { get; init; } = string.Empty;

    public string YearLevel { get; init; } = string.Empty;

    public string DivisionName { get; init; } = string.Empty;

    public DateTime SubmittedAt { get; init; }
}

public sealed record RegistrationApprovalResult(bool Approved, string? Email, string StudentName);
