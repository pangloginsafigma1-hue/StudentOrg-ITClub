using System.Security.Claims;
using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "SuperAdmin")]
public sealed class AdminAccountsController(
    IAdminAccountRepository accountRepository,
    IMemberPortalRepository memberPortalRepository,
    IDivisionRepository divisionRepository,
    ILogger<AdminAccountsController> logger) : Controller
{
    private const int MaxMemberSearchLength = 100;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTerm(int adminId, string position, DateTime termStart, DateTime? termEnd, CancellationToken cancellationToken)
    {
        if (adminId <= 0 || string.IsNullOrWhiteSpace(position) || termStart == default || (termEnd is not null && termEnd < termStart))
            return BadRequest("Invalid administrator term.");
        await accountRepository.RecordPositionTermAsync(adminId, position, termStart, termEnd, cancellationToken);
        TempData["SuccessMessage"] = "Administrator position term recorded.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReserveCandidate(
        string slotKey,
        string? memberId,
        CancellationToken cancellationToken)
    {
        if (!AdminReserveSlots.IsValid(slotKey))
        {
            return BadRequest("Invalid reserve candidate slot.");
        }
        if (memberId is { Length: > 20 })
        {
            return BadRequest("Member ID cannot exceed 20 characters.");
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        {
            return Challenge();
        }

        try
        {
            await accountRepository.SaveReserveNominationAsync(
                slotKey,
                memberId,
                adminId,
                cancellationToken);
            TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(memberId)
                ? "The reserve candidate was removed."
                : "The reserve candidate was saved.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Reserve candidate for slot {SlotKey} could not be saved.", slotKey);
            TempData["ErrorMessage"] = "The reserve candidate could not be saved. Check the database connection and migration.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            SetPageMetadata();
            var administratorAccounts = await accountRepository.GetAllAsync(cancellationToken);
            var superAdminCount = administratorAccounts.Count(account => account.Role == "SuperAdmin");
            var storedNominations = await accountRepository.GetReserveNominationsAsync(cancellationToken);
            var nominationsBySlot = storedNominations.ToDictionary(nomination => nomination.SlotKey);
            var reserveNominations = new List<AdminReserveNomination>();
            foreach (var slot in AdminReserveSlots.All)
            {
                var existing = nominationsBySlot.TryGetValue(slot.Key, out var nomination)
                    ? nomination
                    : new AdminReserveNomination();
                var candidates = (await accountRepository.GetReserveCandidatesAsync(
                    slot.Key,
                    cancellationToken)).ToList();
                if (existing.MemberId is not null
                    && candidates.All(candidate => candidate.MemberId != existing.MemberId))
                {
                    candidates.Add(new AdminReserveCandidate
                    {
                        MemberId = existing.MemberId,
                        FullName = existing.MemberName ?? existing.MemberId
                    });
                }

                reserveNominations.Add(new AdminReserveNomination
                {
                    SlotKey = slot.Key,
                    SlotName = slot.Name,
                    MemberId = existing.MemberId,
                    MemberName = existing.MemberName,
                    CandidateOptions = candidates
                });
            }

            return View(new UserRolesViewModel
            {
                AdministratorAccounts = administratorAccounts,
                ReserveNominations = reserveNominations,
                CanCreateSuperAdmin = superAdminCount < 2
            });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator accounts could not be loaded due to invalid database configuration.");
            SetPageMetadata();
            ViewData["DatabaseError"] = exception.Message;
            return View(new UserRolesViewModel { DatabaseError = exception.Message });
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator accounts could not be loaded from MySQL.");
            SetPageMetadata();
            return View(new UserRolesViewModel
            {
                DatabaseError = "User accounts could not be loaded. Check the database connection and schema."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMemberAccountStatus(
        string memberId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(memberId) || memberId.Length > 20)
        {
            return BadRequest();
        }

        try
        {
            var updated = await memberPortalRepository.SetPortalAccountActiveAsync(
                memberId,
                isActive,
                cancellationToken);
            TempData[updated ? "SuccessMessage" : "ErrorMessage"] = updated
                ? $"Member portal access for {memberId} was {(isActive ? "enabled" : "disabled")}."
                : "This member portal account could not be updated.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member portal account {MemberId} could not be updated.", memberId);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member portal account {MemberId} could not be updated in MySQL.", memberId);
            TempData["ErrorMessage"] = "The member portal account could not be updated. Check the portal database schema.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetMemberPassword(
        ResetMemberPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.NewPassword is null
            || System.Text.Encoding.UTF8.GetByteCount(model.NewPassword) > 72
            || !ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Enter a password of 6 and confirm it correctly.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var updated = await memberPortalRepository.ResetPortalPasswordAsync(
                model.MemberId,
                model.NewPassword,
                cancellationToken);
            TempData[updated ? "SuccessMessage" : "ErrorMessage"] = updated
                ? $"The password for member {model.MemberId} was reset."
                : "This member portal account could not be found.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The password for member portal account {MemberId} could not be reset.", model.MemberId);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The password for member portal account {MemberId} could not be reset in MySQL.", model.MemberId);
            TempData["ErrorMessage"] = "The member password could not be reset. Check the portal database schema.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Create()
    {
        SetPageMetadata();
        return View("Create");
    }

    [HttpGet]
    public async Task<IActionResult> SelectMember(string? search, CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > MaxMemberSearchLength)
        {
            return BadRequest($"Search cannot exceed {MaxMemberSearchLength} characters.");
        }

        try
        {
            SetPageMetadata();
            var canCreateSuperAdmin = await accountRepository.GetSuperAdminCountAsync(cancellationToken) < 2;
            return View("SelectMember", new AdminMemberPickerViewModel
            {
                Search = normalizedSearch,
                CanCreateSuperAdmin = canCreateSuperAdmin,
                Candidates = await accountRepository.GetActiveMemberCandidatesAsync(
                    normalizedSearch,
                    cancellationToken)
            });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Active member candidates could not be loaded due to invalid database configuration.");
            SetPageMetadata();
            return View("SelectMember", new AdminMemberPickerViewModel
            {
                Search = normalizedSearch,
                CanCreateSuperAdmin = false,
                DatabaseError = exception.Message
            });
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Active member candidates could not be loaded from MySQL.");
            SetPageMetadata();
            return View("SelectMember", new AdminMemberPickerViewModel
            {
                Search = normalizedSearch,
                CanCreateSuperAdmin = false,
                DatabaseError = "Active members could not be loaded. Check the database connection and member portal schema."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CreateFromMember(
        string memberId,
        string role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(memberId)
            || memberId.Length > 20
            || role is not ("DivisionAdmin" or "SuperAdmin"))
        {
            return BadRequest();
        }

        try
        {
            var candidate = await accountRepository.GetActiveMemberCandidateAsync(memberId, cancellationToken);
            if (candidate is null)
            {
                TempData["ErrorMessage"] = "That member is no longer active or could not be found.";
                return RedirectToAction(nameof(SelectMember));
            }

            if (role == "DivisionAdmin" && !candidate.CanBeDivisionAdmin)
            {
                TempData["ErrorMessage"] = candidate.DivisionId is null
                    ? "This member has no division and cannot be assigned as a Division Admin."
                    : "This member's division already has an administrator.";
                return RedirectToAction(nameof(SelectMember));
            }
            if (role == "SuperAdmin" && !candidate.CanBeSuperAdmin)
            {
                TempData["ErrorMessage"] = "This member already has an administrator account.";
                return RedirectToAction(nameof(SelectMember));
            }
            if (role == "SuperAdmin"
                && await accountRepository.GetSuperAdminCountAsync(cancellationToken) >= 2)
            {
                TempData["ErrorMessage"] = "Only two Super Admin accounts are allowed.";
                return RedirectToAction(nameof(SelectMember));
            }

            SetPageMetadata();
            return View("Form", new AdminAccountFormViewModel
            {
                FullName = candidate.FullName,
                Email = candidate.Email ?? string.Empty,
                StudentId = candidate.MemberId,
                Course = candidate.Course,
                YearLevel = candidate.YearLevel,
                Position = role == "SuperAdmin" ? "President" : "Division Head",
                DateJoined = candidate.DateJoined,
                Role = role,
                DivisionId = role == "DivisionAdmin" ? candidate.DivisionId : null,
                CanSelectSuperAdmin = role == "SuperAdmin"
                    || await accountRepository.GetSuperAdminCountAsync(cancellationToken) < 2,
                SourceMemberId = candidate.MemberId,
                Divisions = await GetAvailableDivisionsAsync(0, cancellationToken)
            });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator form for member {MemberId} could not be loaded.", memberId);
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(SelectMember));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator form for member {MemberId} could not be loaded from MySQL.", memberId);
            TempData["ErrorMessage"] = "The administrator form could not be loaded. Check the database connection and schema.";
            return RedirectToAction(nameof(SelectMember));
        }
    }

    [HttpGet]
    public async Task<IActionResult> CreateNew(CancellationToken cancellationToken)
    {
        try
        {
            SetPageMetadata();
            var canSelectSuperAdmin = await accountRepository.GetSuperAdminCountAsync(cancellationToken) < 2;
            return View("Form", new AdminAccountFormViewModel
            {
                Divisions = await GetAvailableDivisionsAsync(0, cancellationToken),
                CanSelectSuperAdmin = canSelectSuperAdmin
            });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator account form could not load divisions.");
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator account form could not load divisions from MySQL.");
            TempData["ErrorMessage"] = "The account form could not be loaded. Check the database connection and schema.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        try
        {
            var account = await accountRepository.GetByIdAsync(id, cancellationToken);
            if (account is null)
            {
                return NotFound();
            }

            SetPageMetadata();
            var model = CopyWithDivisions(
                account,
                await GetAvailableDivisionsAsync(id, cancellationToken));
            model.CanSelectSuperAdmin = account.Role == "SuperAdmin"
                || await accountRepository.GetSuperAdminCountAsync(cancellationToken) < 2;
            return View("Form", model);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator {AdminId} could not be loaded due to invalid database configuration.", id);
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator {AdminId} could not be loaded from MySQL.", id);
            TempData["ErrorMessage"] = "The account could not be loaded. Check the database connection and try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminAccountFormViewModel model, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(model.SourceMemberId))
        {
            try
            {
                var candidate = await accountRepository.GetActiveMemberCandidateAsync(
                    model.SourceMemberId,
                    cancellationToken);
                if (candidate is null)
                {
                    ModelState.AddModelError(string.Empty, "The selected member is no longer active or could not be found.");
                }
                else
                {
                    var sourceRole = model.Role;
                    if (sourceRole is not ("DivisionAdmin" or "SuperAdmin"))
                    {
                        ModelState.AddModelError(nameof(model.Role), "Choose a valid administrator role.");
                        sourceRole = "DivisionAdmin";
                    }

                    model.FullName = candidate.FullName;
                    model.StudentId = candidate.MemberId;
                    model.Course = candidate.Course;
                    model.YearLevel = candidate.YearLevel;
                    model.Position = sourceRole == "SuperAdmin" ? "President" : "Division Head";
                    model.DateJoined = candidate.DateJoined;
                    model.Role = sourceRole;
                    model.DivisionId = sourceRole == "DivisionAdmin" ? candidate.DivisionId : null;
                    ModelState.Remove(nameof(model.FullName));
                    ModelState.Remove(nameof(model.StudentId));
                    ModelState.Remove(nameof(model.Course));
                    ModelState.Remove(nameof(model.YearLevel));
                    ModelState.Remove(nameof(model.Position));
                    ModelState.Remove(nameof(model.DateJoined));
                    ModelState.Remove(nameof(model.Role));
                    ModelState.Remove(nameof(model.DivisionId));
                    if (sourceRole == "DivisionAdmin" && !candidate.CanBeDivisionAdmin)
                    {
                        ModelState.AddModelError(nameof(model.DivisionId), "This member's division already has an administrator.");
                    }
                    else if (sourceRole == "SuperAdmin" && !candidate.CanBeSuperAdmin)
                    {
                        ModelState.AddModelError(nameof(model.Role), "This member already has an administrator account.");
                    }
                }
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(exception, "Selected member {MemberId} could not be verified.", model.SourceMemberId);
                ModelState.AddModelError(string.Empty, exception.Message);
            }
            catch (MySqlException exception)
            {
                logger.LogError(exception, "Selected member {MemberId} could not be verified in MySQL.", model.SourceMemberId);
                ModelState.AddModelError(string.Empty, "The selected member could not be verified. Check the database connection and try again.");
            }
        }

        model.FullName = model.FullName.Trim();
        model.Username = model.Username.Trim();
        model.Email = model.Email.Trim();
        if (model.Role == "SuperAdmin")
        {
            model.DivisionId = null;
            model.Position = "President";
        }
        else if (model.Role == "DivisionAdmin")
        {
            model.Position = "Division Head";
        }
        else if (model.Role == "Member")
        {
            model.DivisionId = null;
            model.Position = "Member";
            model.IsActive = false;
        }
        ModelState.Remove(nameof(model.Position));

        IReadOnlyList<Division> divisions;
        try
        {
            var exceptAdminId = model.IsEdit ? model.AdminId : 0;
            divisions = await GetAvailableDivisionsAsync(exceptAdminId, cancellationToken);
            var superAdminCount = await accountRepository.GetSuperAdminCountAsync(cancellationToken);
            model.CanSelectSuperAdmin = model.IsEdit && model.Role == "SuperAdmin"
                || superAdminCount < 2;
            if (model.Role == "DivisionAdmin"
                && model.DivisionId is not int divisionId)
            {
                ModelState.AddModelError(nameof(model.DivisionId), "Choose a valid division.");
            }
            else if (model.Role == "DivisionAdmin"
                && !divisions.Any(division => division.DivisionId == model.DivisionId))
            {
                ModelState.AddModelError(
                    nameof(model.DivisionId),
                    "That division already has a Division Admin. Choose another division.");
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator account division validation failed.");
            ModelState.AddModelError(string.Empty, exception.Message);
            divisions = [];
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator account divisions could not be loaded.");
            ModelState.AddModelError(string.Empty, "Division options could not be loaded. Check the database connection and schema.");
            divisions = [];
        }

        model = CopyWithDivisions(model, divisions);
        if (!ModelState.IsValid)
        {
            SetPageMetadata();
            return View("Form", model);
        }

        try
        {
            var exceptAdminId = model.IsEdit ? model.AdminId : 0;
            if (await accountRepository.IsUsernameTakenAsync(model.Username, exceptAdminId, cancellationToken))
            {
                ModelState.AddModelError(nameof(model.Username), "That username is already taken.");
                SetPageMetadata();
                return View("Form", model);
            }

            if (model.IsEdit)
            {
                await accountRepository.UpdateAsync(model, cancellationToken);
                TempData["SuccessMessage"] = model.Role == "Member"
                    ? $"Administrator access for {model.FullName} was removed and the member record was saved."
                    : $"Administrator account {model.Username} was updated.";
            }
            else
            {
                await accountRepository.CreateAsync(model, cancellationToken);
                TempData["SuccessMessage"] = $"Administrator account {model.Username} was created.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Administrator account {AdminId} update was rejected.", model.AdminId);
            ModelState.AddModelError(string.Empty, exception.Message);
            SetPageMetadata();
            return View("Form", model);
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");
            SetPageMetadata();
            return View("Form", model);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator account {AdminId} could not be saved.", model.AdminId);
            ModelState.AddModelError(string.Empty, "The account could not be saved. Check the database connection and try again.");
            SetPageMetadata();
            return View("Form", model);
        }
    }

    private static AdminAccountFormViewModel CopyWithDivisions(
        AdminAccountFormViewModel model,
        IReadOnlyList<Division> divisions) =>
        new()
        {
            AdminId = model.AdminId,
            CanSelectSuperAdmin = model.CanSelectSuperAdmin,
            SourceMemberId = model.SourceMemberId,
            FullName = model.FullName,
            Username = model.Username,
            Email = model.Email,
            StudentId = model.StudentId,
            Course = model.Course,
            YearLevel = model.YearLevel,
            Position = model.Position,
            DateJoined = model.DateJoined,
            Role = model.Role,
            DivisionId = model.DivisionId,
            Password = model.Password,
            ConfirmPassword = model.ConfirmPassword,
            IsActive = model.IsActive,
            Divisions = divisions
        };

    private async Task<IReadOnlyList<Division>> GetAvailableDivisionsAsync(
        int exceptAdminId,
        CancellationToken cancellationToken)
    {
        var divisions = await divisionRepository.GetAllAsync(cancellationToken);
        var assignedDivisionIds = await accountRepository.GetAssignedDivisionIdsAsync(
            exceptAdminId,
            cancellationToken);
        return divisions
            .Where(division => !assignedDivisionIds.Contains(division.DivisionId))
            .ToArray();
    }

    private void SetPageMetadata()
    {
        ViewData["Title"] = "User Roles";
        ViewData["Description"] = "Review users, roles, and account access.";
        ViewData["ActiveSection"] = "user-roles";
    }
}
