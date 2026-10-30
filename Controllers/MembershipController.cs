using System.Text.RegularExpressions;
using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "SuperAdmin,DivisionAdmin")]
public sealed class MembershipController(
    IMemberRepository memberRepository,
    IDivisionRepository divisionRepository,
    IConfiguration configuration,
    ILogger<MembershipController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        try
        {
            ViewData["Title"] = "Membership";
            ViewData["Description"] = "Add a new student organization member.";
            ViewData["ActiveSection"] = "membership";
            return View("Form", CreateForm(await divisionRepository.GetAllAsync(cancellationToken), UserDivision.GetDivisionId(User)));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Division options could not be loaded due to invalid database configuration.");
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction("Index", "Members");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Division options could not be loaded from MySQL.");
            TempData["ErrorMessage"] = "The membership form could not be loaded. Check the database connection and divisions table.";
            return RedirectToAction("Index", "Members");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var divisionId = UserDivision.GetDivisionId(User);
        Member? member;
        try
        {
            member = await FindMemberAsync(id, divisionId, cancellationToken);
            if (member is null)
            {
                return NotFound();
            }

            var divisions = await divisionRepository.GetAllAsync(cancellationToken);
            ViewData["Title"] = "Membership";
            ViewData["Description"] = "Update an existing member.";
            ViewData["ActiveSection"] = "membership";
            return View("Form", CreateForm(member, divisions, divisionId));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member {MemberId} could not be loaded due to invalid database configuration.", id);
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction("Index", "Members");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member {MemberId} could not be loaded from MySQL.", id);
            TempData["ErrorMessage"] = "The member could not be loaded. Check the database connection and try again.";
            return RedirectToAction("Index", "Members");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        string? id,
        MemberFormViewModel model,
        CancellationToken cancellationToken)
    {
        var divisionScope = UserDivision.GetDivisionId(User);
        if (divisionScope is int assignedDivisionId)
        {
            model.DivisionId = assignedDivisionId;
            ModelState.Remove(nameof(model.DivisionId));
        }

        var isEdit = !string.IsNullOrWhiteSpace(id);
        if (!isEdit)
        {
            model.Status = "Active";
            ModelState.Remove(nameof(model.Status));
        }
        if (isEdit && !string.Equals(id, model.MemberId, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.MemberId), "The membership ID cannot be changed.");
        }

        if (!MemberLookups.Courses.Contains(model.Course, StringComparer.Ordinal)
            || !MemberLookups.YearLevels.Contains(model.YearLevel, StringComparer.Ordinal)
            || !MemberLookups.Positions.Contains(model.Position, StringComparer.Ordinal)
            || (isEdit && !MemberLookups.Statuses.Contains(model.Status, StringComparer.Ordinal)))
        {
            ModelState.AddModelError(string.Empty, "Select a valid course, year level, and position.");
        }
        if (!UserDivision.IsSuperAdmin(User) && !isEdit && model.Position == "Division Head")
        {
            ModelState.AddModelError(nameof(model.Position), "Only a Super Admin can assign the Division Head position.");
        }

        try
        {
            if (model.DivisionId is not int requestedDivisionId
                || !(await divisionRepository.GetAllAsync(cancellationToken))
                    .Any(division => division.DivisionId == requestedDivisionId))
            {
                ModelState.AddModelError(nameof(model.DivisionId), "Choose a valid division.");
            }

            var studentIdPattern = GetStudentIdPattern();
            if (!isEdit && !studentIdPattern.IsMatch(model.MemberId.Trim()))
            {
                ModelState.AddModelError(
                    nameof(model.MemberId),
                    $"Enter a valid membership ID. Use the format {GetStudentIdExample()}.");
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The membership form could not be validated.");
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Division options could not be loaded for membership validation.");
            ModelState.AddModelError(string.Empty, "Division options could not be loaded. Check the database connection and try again.");
        }

        IReadOnlyList<Division> divisions;
        try
        {
            divisions = await divisionRepository.GetAllAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Division options could not be loaded.");
            ModelState.AddModelError(string.Empty, exception.Message);
            divisions = [];
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Division options could not be loaded from MySQL.");
            ModelState.AddModelError(string.Empty, "Division options could not be loaded. Check the database connection and try again.");
            divisions = [];
        }

        if (!ModelState.IsValid)
        {
            SetPageMetadata(isEdit);
            return View("Form", CreateForm(model, isEdit, divisions, divisionScope));
        }

        var member = new Member
        {
            MemberId = model.MemberId.Trim(),
            StudentName = model.StudentName.Trim(),
            Course = model.Course,
            YearLevel = model.YearLevel,
            Position = model.Position,
            Status = model.Status,
            DateJoined = model.DateJoined.Date,
            DivisionId = model.DivisionId
        };

        try
        {
            if (isEdit)
            {
                if (await memberRepository.GetByIdAsync(id!, divisionScope, cancellationToken) is null)
                {
                    return NotFound();
                }

                await memberRepository.UpdateAsync(member, divisionScope, cancellationToken);
                TempData["SuccessMessage"] = $"Member {member.MemberId} was updated.";
            }
            else
            {
                await memberRepository.AddAsync(member, divisionScope, cancellationToken);
                TempData["SuccessMessage"] = $"Member {member.MemberId} was added.";
            }

            return RedirectToAction("Index", "Members");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (MySqlException exception) when (!isEdit && exception.Number == 1062)
        {
            logger.LogInformation(exception, "Member {MemberId} already exists.", member.MemberId);
            ModelState.AddModelError(nameof(model.MemberId), "A member with that ID already exists.");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member {MemberId} could not be saved due to invalid database configuration.", member.MemberId);
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member {MemberId} could not be saved to MySQL.", member.MemberId);
            ModelState.AddModelError(string.Empty, "The member could not be saved. Check the database connection and try again.");
        }

        SetPageMetadata(isEdit);
        return View("Form", CreateForm(model, isEdit, divisions, divisionScope));
    }

    private Task<Member?> FindMemberAsync(string id, int? divisionId, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(id) || id.Length > 20
            ? Task.FromResult<Member?>(null)
            : memberRepository.GetByIdAsync(id, divisionId, cancellationToken);

    private MemberFormViewModel CreateForm(IReadOnlyList<Division> divisions, int? divisionId) =>
        new()
        {
            DivisionId = divisionId,
            Status = "Active",
            Divisions = divisions,
            CanChooseDivision = UserDivision.IsSuperAdmin(User),
            Positions = GetAvailablePositions(isEdit: false),
            StudentIdExample = GetStudentIdExample()
        };

    private MemberFormViewModel CreateForm(
        Member member,
        IReadOnlyList<Division> divisions,
        int? divisionScope) =>
        new()
        {
            MemberId = member.MemberId,
            StudentName = member.StudentName,
            Course = member.Course,
            YearLevel = member.YearLevel,
            Position = member.Position,
            Status = member.Status,
            DateJoined = member.DateJoined,
            DivisionId = divisionScope ?? member.DivisionId,
            Divisions = divisions,
            CanChooseDivision = UserDivision.IsSuperAdmin(User),
            IsEdit = true,
            Positions = GetAvailablePositions(isEdit: true),
            StudentIdExample = GetStudentIdExample()
        };

    private MemberFormViewModel CreateForm(
        MemberFormViewModel submittedModel,
        bool isEdit,
        IReadOnlyList<Division> divisions,
        int? divisionScope) =>
        new()
        {
            MemberId = submittedModel.MemberId,
            StudentName = submittedModel.StudentName,
            Course = submittedModel.Course,
            YearLevel = submittedModel.YearLevel,
            Position = submittedModel.Position,
            Status = submittedModel.Status,
            DateJoined = submittedModel.DateJoined,
            DivisionId = divisionScope ?? submittedModel.DivisionId,
            Divisions = divisions,
            CanChooseDivision = UserDivision.IsSuperAdmin(User),
            IsEdit = isEdit,
            Positions = GetAvailablePositions(isEdit),
            StudentIdExample = GetStudentIdExample()
        };

    private IReadOnlyList<string> GetAvailablePositions(bool isEdit) =>
        !UserDivision.IsSuperAdmin(User) && !isEdit
            ? MemberLookups.Positions.Where(position => position != "Division Head").ToArray()
            : MemberLookups.Positions;

    private void SetPageMetadata(bool isEdit)
    {
        ViewData["Title"] = "Membership";
        ViewData["Description"] = isEdit ? "Update an existing member." : "Add a new student organization member.";
        ViewData["ActiveSection"] = "membership";
    }

    private string GetStudentIdExample() =>
        configuration["StudentIdExample"] ?? "26-0001";

    private Regex GetStudentIdPattern()
    {
        var pattern = configuration["StudentIdPattern"] ?? @"^\d{2}-\d{4}$";
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The StudentIdPattern setting is invalid. Check the configured student ID validation pattern.",
                exception);
        }
    }
}
