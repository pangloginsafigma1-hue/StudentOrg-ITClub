using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "SuperAdmin")]
public sealed class AdminAccountsController(
    IAdminAccountRepository accountRepository,
    IDivisionRepository divisionRepository,
    ILogger<AdminAccountsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            SetPageMetadata();
            return View(await accountRepository.GetAllAsync(cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator accounts could not be loaded due to invalid database configuration.");
            SetPageMetadata();
            ViewData["DatabaseError"] = exception.Message;
            return View(Array.Empty<AdminAccountListItem>());
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator accounts could not be loaded from MySQL.");
            SetPageMetadata();
            ViewData["DatabaseError"] = "Administrator accounts could not be loaded. Check the database connection and schema.";
            return View(Array.Empty<AdminAccountListItem>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        try
        {
            SetPageMetadata();
            return View("Form", new AdminAccountFormViewModel
            {
                Divisions = await divisionRepository.GetAllAsync(cancellationToken)
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
            return View("Form", account);
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
        model.FullName = model.FullName.Trim();
        model.Username = model.Username.Trim();
        model.Email = model.Email.Trim();
        if (model.Role == "SuperAdmin")
        {
            model.DivisionId = null;
        }

        IReadOnlyList<Division> divisions;
        try
        {
            divisions = await divisionRepository.GetAllAsync(cancellationToken);
            if (model.Role == "DivisionAdmin"
                && (model.DivisionId is not int divisionId
                    || !divisions.Any(division => division.DivisionId == divisionId)))
            {
                ModelState.AddModelError(nameof(model.DivisionId), "Choose a valid division.");
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
                TempData["SuccessMessage"] = $"Administrator account {model.Username} was updated.";
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
            FullName = model.FullName,
            Username = model.Username,
            Email = model.Email,
            Role = model.Role,
            DivisionId = model.DivisionId,
            Password = model.Password,
            ConfirmPassword = model.ConfirmPassword,
            IsActive = model.IsActive,
            Divisions = divisions
        };

    private void SetPageMetadata()
    {
        ViewData["Title"] = "Account Management";
        ViewData["Description"] = "Create and manage administrator access.";
        ViewData["ActiveSection"] = "admin-accounts";
    }
}
